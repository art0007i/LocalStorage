using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using FrooxEngine.Store;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.UIX;
using Elements.Core;
using System.IO;
using Newtonsoft.Json;

namespace LocalStorage
{
    public class LocalStorage : ResoniteMod
    {
        public override string Name => "LocalStorage";
        public override string Author => "art0007i";
        public override string Version => "2.0.2";
        public override string Link => "https://github.com/art0007i/LocalStorage/";

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<string> REC_PATH_KEY = new ModConfigurationKey<string>(
            "record_path", "The path in which records will be stored. Changing this setting requires a game restart to apply.",
            () => Path.Combine(Engine.Current.LocalDB.PermanentPath, "LocalStorage", "Records")
        );
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<string> DATA_PATH_KEY = new ModConfigurationKey<string>(
            "data_path", "The path in which item data will be stored. Changing this setting requires a game restart to apply.",
            () => Path.Combine(Engine.Current.LocalDB.PermanentPath, "LocalStorage", "Data")
        );

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            Harmony harmony = new Harmony("me.art0007i.LocalStorage");
            harmony.PatchAll();
        }
        public static ModConfiguration config;

        public const string LOCAL_OWNER = "L-LocalStorage";

        public static bool HIDE_LOCAL = true;
        public static string REC_PATH;
        public static string DATA_PATH;
        public static string ResolveLstore(Uri uri)
        {
            var unsafePath = Path.GetFullPath(DATA_PATH + Uri.UnescapeDataString(uri.AbsolutePath)).Replace('\\', '/'); ;
            if (unsafePath.StartsWith(DATA_PATH))
            {
                if (File.Exists(unsafePath))
                {
                    return unsafePath;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                throw new FileNotFoundException("Unexpected path was received. Path: " + unsafePath + "\nDataPath: " + DATA_PATH);
            }
        }

        [HarmonyPatch(typeof(LocalDB), "Initialize")]
        class LateInitPatch
        {
            public static void Postfix()
            {
                REC_PATH = config.GetValue(REC_PATH_KEY).Replace('\\', '/');
                DATA_PATH = config.GetValue(DATA_PATH_KEY).Replace('\\', '/');
                if (!Directory.Exists(REC_PATH))
                {
                    try { Directory.CreateDirectory(Path.Combine(REC_PATH, "Inventory")); }
                    catch (Exception e)
                    {
                        HIDE_LOCAL = true;
                        Error("A critical error has occured while creating record directory");
                        Error(e);
                    }
                }
                if (!Directory.Exists(DATA_PATH))
                {
                    try { Directory.CreateDirectory(Path.Combine(DATA_PATH, "Inventory")); }
                    catch (Exception e)
                    {
                        HIDE_LOCAL = true;
                        Error("A critical error has occured while creating data directory");
                        Error(e);
                    }
                }
                Msg("Initalized LocalStorage\nData Path: " + DATA_PATH + "\nRecord Path: " + REC_PATH);
                HIDE_LOCAL = false;
            }
        }

        [HarmonyPatch(typeof(InventoryBrowser))]
        class InventoryPatch
        {
			
            [HarmonyPatch("ShowInventoryOwners")]
            [HarmonyPrefix]
            public static bool ShowInventoriesPrefix(InventoryBrowser __instance)
            {
                if (!HIDE_LOCAL && __instance.Engine.Cloud.CurrentUser == null && __instance.CurrentDirectory.OwnerId != LOCAL_OWNER && __instance.World.IsUserspace())
                {

                    RecordDirectory directory = new RecordDirectory(LOCAL_OWNER, "Inventory", __instance.Engine, null);
                    __instance.Open(directory, SlideSwapRegion.Slide.Left);
                    return false;
                } else if(__instance.CurrentDirectory.OwnerId == LOCAL_OWNER && __instance.Engine.Cloud.CurrentUser == null && __instance.World.IsUserspace())
                {
                    Traverse.Create(__instance).Method("TryInitialize").GetValue(null);
                    return false;
                }
                return true;
            }

            [HarmonyPatch("BeginGeneratingNewDirectory")]
            [HarmonyPostfix]
            public static void GenerationPostfix(InventoryBrowser __instance, UIBuilder __result, ref GridLayout folders)
            {
                if (!HIDE_LOCAL && __instance.CurrentDirectory == null && __instance.World.IsUserspace())
                {
                    var builder = __result;
                    builder.NestInto(folders.Slot);
                    var colour = MathX.Lerp(colorX.Lime, colorX.Black, 0.5f);
                    var openFunc = (ButtonEventHandler<string>)AccessTools.Method(typeof(InventoryBrowser), "OpenInventory").CreateDelegate(typeof(ButtonEventHandler<string>), __instance);

                    builder.Button("Local Storage", colour, openFunc, LOCAL_OWNER, __instance.ActualDoublePressInterval);
                    
                    builder.NestOut();
                }
            }

            [HarmonyPatch("OnChanges")]
            [HarmonyPostfix]
            public static void OnChangesPostFix(InventoryBrowser __instance, ref SyncRef<Button> ____inventoriesButton)
            {
                ____inventoriesButton.Target.Enabled = true;
                return;
            }

            [HarmonyPatch("OnCommonUpdate")]
            [HarmonyPostfix]
            public static void OnCommonUpdate(InventoryBrowser __instance)
            {
                if (HIDE_LOCAL &&
                    !__instance.World.IsUserspace() 
                    && __instance.CurrentDirectory != null 
                    && __instance.CurrentDirectory.OwnerId == LOCAL_OWNER)
                {
                    __instance.Open(null, SlideSwapRegion.Slide.Right);
                }
            }
        }

        // TODO: Test on linux
        // High chance it breaks because the resonite inventory uses backslashes internally
        [HarmonyPatch(typeof(RecordDirectory))]
        class RecordDirectoryPatch
        {
            [HarmonyPatch("get_CanWrite")]
            [HarmonyPrefix]
            public static bool CanWrite(ref bool __result, RecordDirectory __instance)
            {
                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    __result = true;
                    return false;
                }
                return true;
            }

            [HarmonyPatch("TryLocalCacheLoad")]
            [HarmonyPrefix]
            public static bool CacheLoad(ref Task<bool> __result, RecordDirectory __instance)
            {
                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    __result = Task<bool>.Run(() => false);
                    return false;
                }
                return true;
            }

            [HarmonyPatch("LoadFrom", new Type[] { typeof(string), typeof(string) })]
            [HarmonyPrefix]
            public static bool LoadFrom(string ownerId, string path, ref Task __result, RecordDirectory __instance)
            {
                if (ownerId == LOCAL_OWNER)
                {
                    __result = Task.Run(() =>
                    {
                        List<RecordDirectory> subdirs = new List<RecordDirectory>();
                        List<Record> recs = new List<Record>();

                        path = path.Replace('\\', '/');
                        var p = Path.Combine(REC_PATH, path);
                        foreach (string dir in Directory.EnumerateDirectories(p))
                        {
                            var dirRec = SkyFrost.Base.RecordHelper.CreateForDirectory<Record>(LOCAL_OWNER, path, Path.GetFileNameWithoutExtension(dir));
                            subdirs.Add(new RecordDirectory(dirRec, __instance, __instance.Engine));
                        }
                        foreach (string file in Directory.EnumerateFiles(p))
                        {
                            if (Path.GetExtension(file) != ".json") continue;

                            var garbo = new Record();

                            var fs = File.ReadAllText(file);
                            // Json parsing is difficult, ok
                            var record = JsonConvert.DeserializeObject<SkyFrost.Base.Record>(fs);

                            garbo.RecordId = record.RecordId;
                            garbo.OwnerId = record.OwnerId;
                            garbo.AssetURI = record.AssetURI;
                            garbo.Name = record.Name;
                            garbo.Description = record.Description;
                            garbo.RecordType = record.RecordType;
                            garbo.OwnerName = record.OwnerName;
                            garbo.Tags = record.Tags;
                            garbo.Path = record.Path;
                            garbo.ThumbnailURI = record.ThumbnailURI;
                            garbo.IsPublic = record.IsPublic;
                            garbo.IsForPatrons = record.IsForPatrons;
                            garbo.IsListed = record.IsListed;
                            garbo.LastModificationTime = record.LastModificationTime;
                            // RootRecordId doesnt exist on CloudX records
                            garbo.CreationTime = record.CreationTime;
                            garbo.FirstPublishTime = record.FirstPublishTime;
                            garbo.Visits = record.Visits;
                            garbo.Rating = record.Rating;
                            garbo.RandomOrder = record.RandomOrder;
                            garbo.Submissions = record.Submissions;
                            garbo.AssetManifest = record.AssetManifest;

                            if(garbo.RecordType == "link")
                            {
                                subdirs.Add(new RecordDirectory(garbo, __instance, __instance.Engine));
                            }
                            else
                            {
                                recs.Add(garbo);
                            }
                        }

                        AccessTools.Field(typeof(RecordDirectory), "subdirectories").SetValue(__instance, subdirs);
                        AccessTools.Field(typeof(RecordDirectory), "records").SetValue(__instance, recs);
                    });
                    return false;
                }
                return true;
            }

            [HarmonyPatch("AddItem")]
            [HarmonyPrefix]
            public static bool AddItem(string name, Uri objectData, Uri thumbnail, IEnumerable<string> tags, ref Record __result, RecordDirectory __instance)
            {
                // TODO: add some unique suffix to prevent overwriting files
                // doing the above will also have an issue of how to delete the files now

                // TODO: add checking for folder gap, so if u try to make an item in a folder that doesn't exist in the file system, make the folders

                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    var fixedPath = __instance.ChildRecordPath.Replace('\\', '/');
                    var savePath = Path.Combine(DATA_PATH, fixedPath, name);
                    var dataPath = savePath + ".json";
                    //var thumbPath = savePath + Path.GetExtension(thumbnail.ToString());

                    Debug("SAVING " + objectData.ToString());

                    var dataTask = __instance.Engine.LocalDB.TryOpenAsset(objectData);
                    dataTask.Wait(); var dataStream = dataTask.Result;
                    

                    var tree = DataTreeConverter.LoadAuto(dataStream);
                    using (var fs = File.CreateText(dataPath))
                    {
                        var wr = new JsonTextWriter(fs);
                        wr.Indentation = 2;
                        wr.Formatting = Formatting.Indented;
                        var writeFunc = AccessTools.Method(typeof(DataTreeConverter), "Write");
                        writeFunc.Invoke(null, new object[] { tree, wr});
                    }
                    /*
                        Currently I have no interest in digging into the internals of the asset variant system
                        and it seems I would need to in order to save thumbnails and assets for items
                        right now they will be stored in either the cache or local db (default behaviour)

                    if (thumbnail != null)
                    {
                        var thumbTask = __instance.Engine.LocalDB.TryOpenAsset(thumbnail);
                        thumbTask.Wait(); var thumbStream = thumbTask.Result;
                        var thumbFile = File.Create(thumbPath);
                        thumbStream.CopyTo(thumbFile);
                        thumbFile.Flush(); thumbFile.Dispose();
                    }
                    */
                    var fileLocalPath = "lstore:///" + fixedPath + "/" + name + ".json";
                    //var thumbLocalPath = "lstore:///" + __instance.ChildRecordPath + "/" + name + Path.GetExtension(thumbnail.ToString());
                    var thumbLocalPath = thumbnail.ToString();

                    var rec = SkyFrost.Base.RecordHelper.CreateForObject<Record>(name, __instance.OwnerId, fileLocalPath, thumbLocalPath);
                    rec.Path = __instance.ChildRecordPath;
                    if (tags != null)
                    {
                        rec.Tags = new HashSet<string>(tags);
                    }

                    var recPath = Path.Combine(REC_PATH, fixedPath, name + ".json");
                    using (var fs = File.CreateText(recPath))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Formatting = Formatting.Indented;
                        serializer.Serialize(fs, rec);
                    }

                    (AccessTools.Field(typeof(RecordDirectory), "records").GetValue(__instance) as List<Record>).Add(rec);
                    __result = rec;
                    return false;
                }
                return true;
            }

            [HarmonyPatch("AddSubdirectory")]
            [HarmonyPrefix]
            public static bool AddSubdirectory(string name, bool dummyOnly, RecordDirectory __instance, ref RecordDirectory __result)
            {
                if(__instance.OwnerId == LOCAL_OWNER)
                {
                    var fixedPath = __instance.ChildRecordPath.Replace('\\', '/');
                    var dirLoc = Path.Combine(REC_PATH, fixedPath , name);
                    if (Directory.Exists(dirLoc))
                    {
                        throw new Exception("Subdirectory with name '" + name + "' already exists.");
                    }
                    var rec = SkyFrost.Base.RecordHelper.CreateForDirectory<Record>(LOCAL_OWNER, __instance.ChildRecordPath, name);
                    if (!dummyOnly)
                    {
                        Directory.CreateDirectory(dirLoc);
                        Directory.CreateDirectory(Path.Combine(DATA_PATH, fixedPath, name));
                    }
                    var ret = new RecordDirectory(rec, __instance, __instance.Engine);
                    (AccessTools.Field(typeof(RecordDirectory), "subdirectories").GetValue(__instance) as List<RecordDirectory>).Add(ret);
                    __result = ret;
                    return false;
                }
                return true;
            }

            [HarmonyPatch("AddLinkAsync")]
            [HarmonyPrefix]
            public static bool AddLinkAsync(string name, Uri target, RecordDirectory __instance, ref Task<Record> __result)
            {
                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    var fixedPath = __instance.ChildRecordPath.Replace('\\', '/');
                    Record record = SkyFrost.Base.RecordHelper.CreateForLink<Record>(name, __instance.OwnerId, target.ToString(), null);
                    record.Path = __instance.ChildRecordPath;

                    var recPath = Path.Combine(REC_PATH, fixedPath, name + ".json");
                    using (var fs = File.CreateText(recPath))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Formatting = Formatting.Indented;
                        serializer.Serialize(fs, record);
                    }

                    RecordDirectory item = new RecordDirectory(record, __instance, __instance.Engine);
                    (AccessTools.Field(typeof(RecordDirectory), "subdirectories").GetValue(__instance) as List<RecordDirectory>).Add(item);
                    __result = Task<Record>.Run(()=>record);
                    return false;
                }
                return true;
            }

            [HarmonyPatch("SetPublicRecursively")]
            [HarmonyPrefix]
            public static bool SetPublicRecursively(RecordDirectory __instance)
            {
                if(__instance.OwnerId == LOCAL_OWNER)
                {
                    throw new Exception("You cannot set public on a local directory.");
                }
                return true;
            }

            [HarmonyPatch("DeleteItem")]
            [HarmonyPrefix]
            public static bool DeleteItem(Record record, RecordDirectory __instance, ref bool __result)
            {
                if(__instance.OwnerId == LOCAL_OWNER)
                {
                    var test = (AccessTools.Field(typeof(RecordDirectory), "records").GetValue(__instance) as List<Record>).Remove(record);
                    if (test)
                    {
                        var asset = new Uri(record.AssetURI);
                        if (asset.Scheme == "lstore")
                        {
                            var file = ResolveLstore(asset);
                            File.Delete(file);
                        }

                        // this is not the smartest system,
                        // if a user manually creates a record that is in the wrong path and wrong name it could be bad
                        File.Delete(Path.Combine(REC_PATH, record.Path.Replace('\\', '/'), record.Name) + ".json");
                    }
                    __result = test;
                    return false;
                }
                return true;
            }

            [HarmonyPatch("DeleteSubdirectory")]
            [HarmonyPrefix]
            public static bool DeleteSubdirectory(RecordDirectory directory, RecordDirectory __instance)
            {
                if(__instance.OwnerId == LOCAL_OWNER)
                {
                    var subs = (AccessTools.Field(typeof(RecordDirectory), "subdirectories").GetValue(__instance) as List<RecordDirectory>);
                    var test = subs.Remove(directory);
                    if (!test)
                    {
                        throw new Exception("Directory doesn't contain given subdirectory");
                    }
                    _ = RecursiveDelete(directory);
                    return false;
                }
                return true;
            }
            public static async Task RecursiveDelete(RecordDirectory dir)
            {
                if (dir.CanWrite && !dir.IsLink)
                {
                    await dir.EnsureFullyLoaded();
                    foreach(var subdir in dir.Subdirectories.ToList())
                    {
                        await RecursiveDelete(subdir);
                    }
                    foreach(var rec in dir.Records.ToList())
                    {
                        dir.DeleteItem(rec);
                    }
                    if(dir.DirectoryRecord != null)
                    {
                        Directory.Delete(Path.Combine(REC_PATH, dir.Path.Replace('\\', '/')));
                        Directory.Delete(Path.Combine(DATA_PATH, dir.Path.Replace('\\', '/')));
                    }
                }
                if(dir.LinkRecord != null)
                {
                    File.Delete(Path.Combine(REC_PATH, dir.LinkRecord.Path.Replace('\\', '/'), dir.LinkRecord.Name) + ".json");
                }
            }
        }
        [HarmonyPatch(typeof(AssetManager))]
        class AssetManagerPatch
        {
            [HarmonyPatch("GatherAssetFile")]
            [HarmonyPrefix]
            public static bool RequestGather(Uri assetURL, ref ValueTask<string> __result)
            {
                if (assetURL.Scheme == "lstore")
                {
                    __result = new ValueTask<string>(ResolveLstore(assetURL));
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(DataTreeConverter), nameof(DataTreeConverter.Load), new Type[] { typeof(string), typeof(string) } )]
        class JsonSupportAdding
        {
            public static bool Prefix(string file, string ext, ref DataTreeDictionary __result)
            {
                if(ext == null)
                {
                    ext = Path.GetExtension(file).ToLower().Replace(".", "");
                }
                if (ext == "json")
                {
                    using (var fileReader = File.OpenText(file))
                    using (var jsonReader = new JsonTextReader(fileReader))
                    {
                        var readFunc = AccessTools.Method(typeof(DataTreeConverter), "Read");
                        __result = (DataTreeDictionary)readFunc.Invoke(null, new object[] { jsonReader });
                    }
                    return false;
                }
                return true;
            }
        }

        /*
         * World saving stuff, currently completely broken
         * 
        [HarmonyPatch(typeof(Userspace))]
        class UserspacePatch
        {
            [HarmonyPatch("SaveWorldTaskIntern")]
            [HarmonyPrefix]
            public static bool SaveWorldTaskIntern(World world, Record record, RecordOwnerTransferer transferer, ref Task<Record> __result, Userspace __instance)
            {
                if(record.OwnerId == LOCAL_OWNER)
                {
                    __result = Task.Run(() =>
                    {
                        if (record == null)
                        {
                            throw new Exception("World record is null, cannot perform save");
                        }
                        TaskCompletionSource<SavedGraph> completionSource = new TaskCompletionSource<SavedGraph>();
                        string _name = null;
                        string _description = null;
                        HashSet<string> _tags = null;
                        world.RunSynchronously(delegate
                        {
                            try
                            {
                                int num = MaterialOptimizer.DeduplicateMaterials(world);
                                int num2 = WorldOptimizer.DeduplicateStaticProviders(world);
                                int num3 = WorldOptimizer.CleanupAssets(world, true, WorldOptimizer.CleanupMode.MarkNonpersistent);
                                Msg(string.Format("World Optimized! Deduplicated Materials: {0}, Deduplicated Static Providers: {1}, Cleaned Up Assets: {2}", num, num2, num3));
                                completionSource.SetResult(world.SaveWorld());
                                _name = world.Name;
                                _description = world.Description;
                                _tags = new HashSet<string>();
                                foreach (string text in world.Tags)
                                {
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        _tags.Add(text);
                                    }
                                }
                            }
                            catch (Exception exception)
                            {
                                completionSource.SetException(exception);
                            }
                        }, false, null, false);
                        var t0 = completionSource.Task; t0.Wait();
                        SavedGraph savedGraph = t0.Result;
                        SavedGraph graph = savedGraph;
                        //await default(ToBackground);
                        Record result;
                        try
                        {
                            if (transferer == null)
                            {
                                transferer = new RecordOwnerTransferer(__instance.Engine, record.OwnerId, null);
                            }
                            var t1 = transferer.EnsureOwnerId(graph);
                            t1.Wait();
                            DataTreeSaver dataTreeSaver = new DataTreeSaver(__instance.Engine);
                            SavedGraph graph2 = graph;
                            IWorldLink sourceLink = world.SourceLink;
                            var t3 = dataTreeSaver.SaveLocally(graph2, (sourceLink != null) ? sourceLink.URL : null); t0.Wait();
                            Uri uri = t3.Result;
                            Debug(uri);
                            if (!record.IsPublic)
                            {
                                World parent = world.Parent;
                                bool? flag;
                                if (parent == null)
                                {
                                    flag = null;
                                }
                                else
                                {
                                    Record correspondingRecord = parent.CorrespondingRecord;
                                    flag = ((correspondingRecord != null) ? new bool?(correspondingRecord.IsPublic) : null);
                                }
                                bool? flag2 = flag;
                                record.IsPublic = flag2.GetValueOrDefault();
                            }
                            record.Name = _name;
                            record.Description = _description;
                            record.Tags = _tags;
                            record.AssetURI = uri.ToString();
                            record.RecordType = "world";
                            Uri sourceURL = SkyFrost.Base.RecordUtil.GenerateUri(record.OwnerId, record.RecordId);
                            if (world.CorrespondingRecord == record)
                            {
                                world.SourceURL = sourceURL;
                            }
                            string worldInfo = string.Format("Name: {0}. RecordId: {1}:{2}. Local: {3}, Global: {4}", new object[] { record.Name, record.OwnerId, record.RecordId, record.LocalVersion, record.GlobalVersion });

                            var p = "";
                            if(record.Path == null)
                            {
                                p = Path.Combine(DATA_PATH, record.Path, record.Name);
                            }
                            else
                            {
                                p = Path.Combine(DATA_PATH, record.Name);
                            }
                            Msg(record.Path);
                            DataTreeConverter.Save(graph.Root, p + ".json");
                            var recPath = Path.Combine(REC_PATH, record.Path, record.Name + ".json");
                            using (var fs = File.CreateText(recPath))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                serializer.Formatting = Formatting.Indented;
                                serializer.Serialize(fs, record);
                            }

                            result = record;
                            
                        }
                        catch (Exception ex)
                        {
                            string tempFilePath = __instance.Engine.LocalDB.GetTempFilePath(".lz4bson");
                            DataTreeConverter.Save(graph.Root, tempFilePath);
                            Error(string.Concat(new string[]
                            {
                                "Exception in the save process for ",
                                _name,
                                "!\nDumping the raw save data to: ",
                                tempFilePath,
                                "\n",
                                (ex != null) ? ex.ToString() : null
                            }));
                            result = null;
                        }
                        return result;
                    });
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(RecordOwnerTransferer))]
        class TransererPatch{
            [HarmonyPatch("ShouldProcess")]
            [HarmonyPrefix]
            public static bool ShouldTransfer(string ownerId, string recordId, RecordOwnerTransferer __instance)
            {
                Debug(ownerId);
                Debug(__instance.TargetOwnerID);
                Debug(__instance.SourceRootOwnerID);
                //return false;
                return true;
            }
        }
        */
    }
}