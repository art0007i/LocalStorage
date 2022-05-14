using HarmonyLib;
using NeosModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.UIX;
using BaseX;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using LZ4;

namespace LocalStorage
{
    public class LocalStorage : NeosMod
    {
        public override string Name => "LocalStorage";
        public override string Author => "art0007i";
        public override string Version => "1.0.0";
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
            Harmony harmony = new Harmony("me.art0007i.LocalStorage");
            harmony.PatchAll();

            config = GetConfiguration();
        }
        public static ModConfiguration config;

        public const string LOCAL_OWNER = "L-LocalStorage";

        public static bool HIDE_LOCAL = true;
        public static string REC_PATH;
        public static string DATA_PATH;
        public static string ResolveLstore(Uri uri)
        {
            var unsafePath = DATA_PATH + Uri.UnescapeDataString(uri.AbsolutePath);
            if (Path.GetFullPath(unsafePath).StartsWith(DATA_PATH))
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
                throw new FileNotFoundException("Unexpected path was received. Path: " + unsafePath);
            }
        }

        [HarmonyPatch(typeof(LocalDB), "Initialize")]
        class LateInitPatch
        {
            public static void Postfix()
            {
                REC_PATH = config.GetValue(REC_PATH_KEY);
                DATA_PATH = config.GetValue(DATA_PATH_KEY);
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
                Debug("Initalized LocalStorage\nData Path: " + DATA_PATH + "\nRecord Path: " + REC_PATH);
                HIDE_LOCAL = false;
            }
        }

        [HarmonyPatch(typeof(InventoryBrowser))]
        class InventoryPatch
        {
            // TODO: allow non logged in users to see the local storage (cloud button is unavailable)
            [HarmonyPatch("BeginGeneratingNewDirectory")]
            [HarmonyPostfix]
            public static void GenerationPostfix(InventoryBrowser __instance, UIBuilder __result)
            {
                if (!HIDE_LOCAL && __instance.CurrentDirectory == null && __instance.World.IsUserspace())
                {
                    var builder = __result;
                    var colour = MathX.Lerp(color.Lime, color.White, 0.5f);
                    var openFunc = (ButtonEventHandler<string>)AccessTools.Method(typeof(InventoryBrowser), "OpenInventory").CreateDelegate(typeof(ButtonEventHandler<string>), __instance);

                    builder.Button("Local Storage", colour, openFunc, LOCAL_OWNER, __instance.ActualDoublePressInterval);
                }
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
        // High chance it breaks because the neos inventory uses backslashes internally
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

                        foreach (string dir in Directory.EnumerateDirectories(Path.Combine(REC_PATH, path)))
                        {
                            var dirRec = CloudX.Shared.RecordHelper.CreateForDirectory<Record>(LOCAL_OWNER, path, Path.GetFileNameWithoutExtension(dir));
                            subdirs.Add(new RecordDirectory(dirRec, __instance, __instance.Engine));
                        }
                        foreach (string file in Directory.EnumerateFiles(Path.Combine(REC_PATH, path)))
                        {
                            if (Path.GetExtension(file) != ".json") continue;

                            var garbo = new Record();

                            var fs = File.ReadAllText(file);
                            // Json parsing is difficult, ok
                            var record = JsonConvert.DeserializeObject<CloudX.Shared.Record>(fs);

                            garbo.RecordId = record.RecordId;
                            garbo.OwnerId = record.OwnerId;
                            garbo.AssetURI = record.AssetURI;
                            garbo.GlobalVersion = record.GlobalVersion;
                            garbo.LocalVersion = record.LocalVersion;
                            garbo.LastModifyingUserId = record.LastModifyingUserId;
                            garbo.LastModifyingMachineId = record.LastModifyingMachineId;
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
                            garbo.NeosDBManifest = record.NeosDBManifest;

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
                    var savePath = Path.Combine(DATA_PATH, __instance.ChildRecordPath, name);
                    var dataPath = savePath + ".json";
                    var thumbPath = savePath + Path.GetExtension(thumbnail.ToString());

                    var dataTask = __instance.Engine.LocalDB.TryOpenAsset(objectData);
                    dataTask.Wait(); var dataStream = dataTask.Result;
                    using (var ms = new LZ4Stream(dataStream, LZ4StreamMode.Decompress, LZ4StreamFlags.None))
                    using (BsonDataReader reader = new BsonDataReader(ms))
                    {
                        // Use this for 7zBSON files
                        // whenever I try to save anything it seems to be lz4BSON so dont need it rn
                        // SevenZip.Helper.Decompress(dataStream, ms);

                        JsonSerializer serializer = new JsonSerializer();
                        var ser = serializer.Deserialize(reader);
                        serializer.Formatting = Formatting.Indented;
                        using (var fs = File.CreateText(dataPath))
                        {
                            serializer.Serialize(fs, ser);
                        }
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
                    var fileLocalPath = "lstore:///" + __instance.ChildRecordPath + "/" + name + ".json";
                    //var thumbLocalPath = "lstore:///" + __instance.ChildRecordPath + "/" + name + Path.GetExtension(thumbnail.ToString());
                    var thumbLocalPath = thumbnail.ToString();

                    var rec = CloudX.Shared.RecordHelper.CreateForObject<Record>(name, __instance.OwnerId, fileLocalPath, thumbLocalPath);
                    rec.Path = __instance.ChildRecordPath;
                    if (tags != null)
                    {
                        rec.Tags = new HashSet<string>(tags);
                    }

                    var recPath = Path.Combine(REC_PATH, __instance.ChildRecordPath, name + ".json");
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
                    var dirLoc = Path.Combine(REC_PATH, __instance.ChildRecordPath , name);
                    if (Directory.Exists(dirLoc))
                    {
                        throw new Exception("Subdirectory with name '" + name + "' already exists.");
                    }
                    var rec = CloudX.Shared.RecordHelper.CreateForDirectory<Record>(LOCAL_OWNER, __instance.ChildRecordPath, name);
                    if (!dummyOnly)
                    {
                        Directory.CreateDirectory(dirLoc);
                        Directory.CreateDirectory(Path.Combine(DATA_PATH, __instance.ChildRecordPath, name));
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
                    Record record = CloudX.Shared.RecordHelper.CreateForLink<Record>(name, __instance.OwnerId, target.ToString(), null);
                    record.Path = __instance.ChildRecordPath;

                    var recPath = Path.Combine(REC_PATH, __instance.ChildRecordPath, name + ".json");
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
                        File.Delete(Path.Combine(REC_PATH, record.Path, record.Name) + ".json");
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
                        Directory.Delete(Path.Combine(REC_PATH, dir.Path));
                        Directory.Delete(Path.Combine(DATA_PATH, dir.Path));
                    }
                }
                if(dir.LinkRecord != null)
                {
                    File.Delete(Path.Combine(REC_PATH, dir.LinkRecord.Path, dir.LinkRecord.Name) + ".json");
                }
            }
        }
        [HarmonyPatch(typeof(AssetManager))]
        class AssetManagerPatch
        {
            [HarmonyPatch("RequestGather")]
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
    }
}