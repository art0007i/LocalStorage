using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.UIX;
using Elements.Core;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace LocalStorage
{
    public class LocalStorage : ResoniteMod
    {
        public override string Name => "LocalStorage";
        public override string Author => "CalamityLime";
        public override string Version => "2.0.3";
        public override string Link => "https://youtu.be/dQw4w9WgXcQ";



        // ------------------------------------------------------------------------------------------ //
        /* ========== Register Mod Config Data/Keys ========== */

        private static ModConfiguration Config;

        public const string LOCAL_OWNER = "L-LocalStorage";
        public static bool HIDE_LOCAL = true;
        public static string REC_PATH;
        public static string DATA_PATH;
        public static string THUMB_PATH;

        // ===== Record Path
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> REC_PATH_KEY = new ModConfigurationKey<string>( 
            "record_path", "The path in which records will be stored. Changing this setting requires a game restart to apply.", 
            () => Path.Combine(Engine.Current.LocalDB.PermanentPath, "LocalStorage", "Records") 
        );

        // ===== Data Path
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> DATA_PATH_KEY = new ModConfigurationKey<string>(
            "data_path", "The path in which item data will be stored. Changing this setting requires a game restart to apply.",
            () => Path.Combine(Engine.Current.LocalDB.PermanentPath, "LocalStorage", "Data")
        );

        // ===== Thumbnail Path
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<string> THUMB_PATH_KEY = new ModConfigurationKey<string>(
            "thumb_path", "The path in which item thumbnails will be stored. Changing this setting requires a game restart to apply.",
            () => Path.Combine(Engine.Current.LocalDB.PermanentPath, "LocalStorage", "Thumbnail")
        );


        // ------------------------------------------------------------------------------------------ //
        /* ========== Our Hook into the game ========== */

        public override void OnEngineInit()
        {
            Config = GetConfiguration(); //Get this mods' current ModConfiguration

            Harmony harmony = new Harmony("me.CalamityLime.LocalStorage");
            harmony.PatchAll();
        }



        // ------------------------------------------------------------------------------------------ //
        /* ========== Our Custom Functions ========== */
        // ------------------------------------------------------------------------------------------ //

        public static string ResolveLstorePath(Uri uri, string path)
        {
            var unsafePath = Path.GetFullPath(path + Uri.UnescapeDataString(uri.AbsolutePath)).Replace('\\', '/'); ;
            if (unsafePath.StartsWith(path))
            {
                if (File.Exists(unsafePath))    { return unsafePath; }
                else                            { return null; }
            }
            else
            {
                throw new FileNotFoundException("Unexpected path was received. Looking for path: " + path + "\nFound path: " + unsafePath);
            }
        }



        // ===== Clean up the File name a bit since Resonite allows a lot of tradiitionally invalid characters as in file names
        // Strip out invailid characters to make the Resonite item more likely to save correctly.

        // --- Blacklist of chars which you cannot have in file names
        private static readonly char[] blacklist = { '<', '>', '"', '\'', '\\', '/', '%', '{', '}', '*', '?', ':', '|'};

        // --- Define the regular expression pattern to match emojis
        private static readonly string emojiPattern = @"\p{So}";

        // --- Try to force filename to contain valid characters only
        private static string StripInvalidCharacters(string input)
        {
            // ===  Replace emojis with an empty string
            input = Regex.Replace(input, emojiPattern, string.Empty);

            // === Create a new string builder to store the result
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            // === Iterate through each character in the input string
            foreach (char c in input)
            {
                // = If the character not in blacklist, then append to result string
                if (Array.IndexOf(blacklist, c) == -1) 
                { 
                    builder.Append(c); 
                }
            }

            // === Return the result as a string
            return builder.ToString();
        }



        /// <summary>
        ///  Attempt 2 at an ID generator
        /// </summary>
        // Code "borrowed" from this => https://github.com/pavelsource/shorter-guid 

        private static char[] dictionary =
        {
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', '-'
        };  // RFC 4648 Base64

        private const int _bitMask = 63;
        private const int _bitShift = 6; // 5 bits max value is 32, i.e. we need 32 chars to encode 5 bits
        private const int _bitsInByte = 8;
        private const int _outputLength = 21; // (128 bits in Guid / 6 bits) = 21 chars


        public static string ToShorterGUID(string guid)
        {
            var utf8 = new UTF8Encoding();
            var data = utf8.GetBytes(guid);
            System.Text.StringBuilder result = new StringBuilder(_outputLength);

            var last = data.Length;
            var offset = 0;
            int buffer = data[offset++];
            var bitsLeft = _bitsInByte;
            while (bitsLeft > 0 || offset < last)
            {
                if (bitsLeft < _bitShift)
                {
                    if (offset < last)
                    {
                        buffer <<= _bitsInByte;
                        buffer |= (data[offset++] & byte.MaxValue);
                        bitsLeft += _bitsInByte;
                    }
                    else
                    {
                        int pad = _bitShift - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }
                int index = _bitMask & (buffer >> (bitsLeft - _bitShift));
                bitsLeft -= _bitShift;
                result.Append(dictionary[index]);
            }

            return result.ToString();
        }



        // ------------------------------------------------------------------------------------------ //
        /* ========== Our Harmony Patchs ========== */

        // ===== Database init patch that forces the game to load our local store as an in game accessable database. 
        [HarmonyPatch(typeof(FrooxEngine.Store.LocalDB), nameof(FrooxEngine.Store.LocalDB.Initialize))]
        class LateInitPatch
        {
            public static void Postfix()
            {
                REC_PATH = Config.GetValue(REC_PATH_KEY).Replace('\\', '/');
                DATA_PATH = Config.GetValue(DATA_PATH_KEY).Replace('\\', '/');
                THUMB_PATH = Config.GetValue(THUMB_PATH_KEY).Replace('\\', '/');

                if (Directory.Exists(REC_PATH) is false)
                {
                    try { Directory.CreateDirectory(Path.Combine(REC_PATH, "Inventory")); }
                    catch (Exception e)
                    {
                        HIDE_LOCAL = true;
                        Error("A critical error has occured while creating record directory");
                        Error(e);
                    }
                }
                if (Directory.Exists(DATA_PATH) is false)
                {
                    try { Directory.CreateDirectory(Path.Combine(DATA_PATH, "Inventory")); }
                    catch (Exception e)
                    {
                        HIDE_LOCAL = true;
                        Error("A critical error has occured while creating data directory");
                        Error(e);
                    }
                }
                if (Directory.Exists(THUMB_PATH) is false)
                {
                    try { Directory.CreateDirectory(Path.Combine(THUMB_PATH, "Inventory")); }
                    catch (Exception e)
                    {
                        HIDE_LOCAL = true;
                        Error("A critical error has occured while creating thumbnail directory");
                        Error(e);
                    }
                }

                Msg("Initalized LocalStorage\nData Path: " + DATA_PATH + "\nRecord Path: " + REC_PATH + "\nThumbnail Path: " + THUMB_PATH);
                HIDE_LOCAL = false;
            }
        }


        // ===== This adds out local inventory to the various inventories resonite seems to support. 
        [HarmonyPatch(typeof(InventoryBrowser))]
        class InventoryPatch
        {
            // Appearently there is a button for this somewhere. 
            [HarmonyPatch("ShowInventoryOwners")]
            [HarmonyPrefix]
            public static bool ShowInventoriesPrefix(InventoryBrowser __instance)
            {
                if (!HIDE_LOCAL && __instance.Engine.Cloud.CurrentUser == null && __instance.CurrentDirectory.OwnerId != LOCAL_OWNER && __instance.World.IsUserspace())
                {
                    RecordDirectory directory = new RecordDirectory(LOCAL_OWNER, "Inventory", __instance.Engine, null);
                    __instance.Open(directory, SlideSwapRegion.Slide.Left);
                    return false;
                }
                else if (__instance.CurrentDirectory.OwnerId == LOCAL_OWNER && __instance.Engine.Cloud.CurrentUser == null && __instance.World.IsUserspace())
                {
                    Traverse.Create(__instance).Method("TryInitialize").GetValue(null);
                    return false;
                }
                return true;
            }

            // Add our Local Store button to the inventory browser options.
            [HarmonyPatch("BeginGeneratingNewDirectory")]
            [HarmonyPostfix]
            public static void GenerationPostfix(InventoryBrowser __instance, UIBuilder __result, ref GridLayout folders)
            {
                if (!HIDE_LOCAL && __instance.CurrentDirectory == null && __instance.World.IsUserspace())
                {
                    var builder = __result;
                    builder.NestInto(folders.Slot);

                    //var colour = MathX.Lerp(new Elements.Core.colorX(0.419608f, 0.725490f, 0.996078f), colorX.Black, 0.5f);
                    var colour = MathX.Lerp(new Elements.Core.colorX(0.95683f, 0.529411f, 0.529411f), colorX.Black, 0.5f);

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
            // ===== Return true assuming we can write to our local store
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

            // ===== I think this tells the game not to load shit?
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

            // ===== Load our local storeage files as an RecordDiretory for Resonite
            [HarmonyPatch("LoadFrom", new Type[] { typeof(string), typeof(string) })]
            [HarmonyPrefix]
            public static bool LoadFrom(string ownerId, string path, ref Task __result, RecordDirectory __instance)
            {
                if (ownerId == LOCAL_OWNER)
                {
                    __result = Task.Run(() =>
                    {
                        List<FrooxEngine.RecordDirectory> subdirs = new List<FrooxEngine.RecordDirectory>();
                        List<global::FrooxEngine.Store.Record> recs = new List<global::FrooxEngine.Store.Record>();

                        path = path.Replace('\\', '/');
                        string clean_p = Path.Combine(REC_PATH, path);

                        foreach (string dir in Directory.EnumerateDirectories(clean_p))
                        {
                            var dirRec = SkyFrost.Base.RecordHelper.CreateForDirectory<FrooxEngine.Store.Record>(LOCAL_OWNER, path, Path.GetFileNameWithoutExtension(dir));
                            subdirs.Add(new RecordDirectory(dirRec, __instance, __instance.Engine));
                        }

                        foreach (string file in Directory.EnumerateFiles(clean_p))
                        {
                            // Skip if not json file
                            if (Path.GetExtension(file) != ".json") continue;

                            var garbo = new global::FrooxEngine.Store.Record();

                            var fs = File.ReadAllText(file);
                            // Json parsing is difficult, ok, => ok!
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
                            garbo.IsDeleted = record.IsDeleted;
                            garbo.Visits = record.Visits;
                            garbo.Rating = record.Rating;
                            garbo.RandomOrder = record.RandomOrder;
                            garbo.Submissions = record.Submissions;
                            garbo.MigrationMetadata = record.MigrationMetadata;
                            garbo.AssetManifest = record.AssetManifest;

                            if (garbo.RecordType == "link")
                            {
                                subdirs.Add(new RecordDirectory(garbo, __instance, __instance.Engine));
                            } else {
                                recs.Add(garbo);
                            }
                        }

                        AccessTools.Field(typeof(RecordDirectory), "subdirectories").SetValue(__instance, subdirs);
                        AccessTools.Field(typeof(RecordDirectory), "records").SetValue(__instance, recs);
                    });
                    return false;
                }
                return true; // Fun original Function
            }

            [HarmonyPatch("AddItem")]
            [HarmonyPrefix]
            public static bool AddItem(string name, Uri objectData, Uri thumbnail, IEnumerable<string> tags, ref global::FrooxEngine.Store.Record __result, RecordDirectory __instance)
            {
                // TODO: add checking for folder gap, so if u try to make an item in a folder that doesn't exist in the file system, make the folders

                // ===== If selected inventory is our local store
                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    // Create new GUID for use as a record id for the Resonite record
                    string recordId = Guid.NewGuid().ToString();

                    // Crunch the GUID to 21 character string to use as a name
                    string recordname = ToShorterGUID(recordId);

                    // --- Fix the path so it'll actually save, this seems to just be a folder dir thing.
                    string fixedCRPath = __instance.ChildRecordPath.Replace('\\', '/');

                    // --- Local Store : Data Path store
                    string dataPath = Path.Combine(DATA_PATH, fixedCRPath, (recordname + ".json"));

                    // --- Local Store : Thumbnail Path store
                    string thumbPath = Path.Combine(THUMB_PATH, fixedCRPath, (recordname + Path.GetExtension(thumbnail.ToString())));

                    // --- Local Store : Record Path store
                    string recPath = Path.Combine(REC_PATH, fixedCRPath, (recordname + ".json"));

                    // --- Record Store : Path written to FrooxEngine.Store.Record so Resonite knows where the data is 
                    string fileLocalPath = "lstore:///" + fixedCRPath + "/" + recordname + ".json";

                    // --- Record Store : Path written to FrooxEngine.Store.Record so Resonite knows where the thumbnail is
                    string thumbLocalPath = "lstorethumb:///" + fixedCRPath + "/" + recordname + Path.GetExtension(thumbnail.ToString());


                    // ========================================
                    // --- Write message to Resonite logs, which you should delete regularly because the staff leave them build up adding bloat to the install dir
                    // --- as well as having a history of including data which should not be included in logs such as your user token which would allow someone to 
                    // --- log in as you without your password. 
                    Msg("SAVING " + objectData.ToString());


                    // ========================================
                    // --- Fetch the Object data
                    Task<System.IO.Stream> dataTask = __instance.Engine.LocalDB.TryOpenAsset(objectData);
                    dataTask.Wait();
                    DataTreeDictionary dataTreeDir = DataTreeConverter.LoadAuto(dataTask.Result);


                    // ========================================
                    // --- Write Data to our Local Store
                    using (StreamWriter fs = File.CreateText(dataPath))
                    {
                        JsonTextWriter wr = new JsonTextWriter(fs);
                        wr.Indentation = 2;
                        wr.Formatting = Newtonsoft.Json.Formatting.Indented;
                        AccessTools.Method(typeof(DataTreeConverter), "Write", null, null).Invoke(null, new object[] { dataTreeDir, wr });
                    }


                    // ========================================
                    // --- If Thumbnail not null, then write our thumbnail to our local store
                    if (thumbnail != null)
                    {
                        Task<System.IO.Stream> thumbTask = __instance.Engine.LocalDB.TryOpenAsset(thumbnail);
                        thumbTask.Wait();
                        var thumbStream = thumbTask.Result;
                        var thumbFile = File.Create(thumbPath);
                        thumbStream.CopyTo(thumbFile);
                        thumbFile.Flush(); 
                        thumbFile.Dispose();
                    }


                    // ========================================
                    // --- Create the FrooxEngine.Store.Record item
                    var rec = SkyFrost.Base.RecordHelper.CreateForObject<global::FrooxEngine.Store.Record>(name, __instance.OwnerId, fileLocalPath, thumbLocalPath, ("R-" + recordId));
                    rec.Path = __instance.ChildRecordPath;

                    // --- Convert tags into something less shit than what resonite uses
                    if (tags != null) { rec.Tags = new HashSet<string>(tags); }

                    // --- Write our record json to file
                    using (StreamWriter fs = File.CreateText(recPath))
                    {
                        new JsonSerializer
                        {
                            Formatting = Newtonsoft.Json.Formatting.Indented
                        }.Serialize(fs, rec);
                    }

                    // --- Add our created FrooxEngine.Store.Record to the array of available records, effectively spoon feeding Resonite data it needs to be relivant to an audience.
                    (AccessTools.Field(typeof(RecordDirectory), "records").GetValue(__instance) as List<FrooxEngine.Store.Record>).Add(rec);
                    __result = rec;
                    return false; // Skip original function
                }

                // ========== Exit if (__instance.OwnerId == LOCAL_OWNER) Check
                return true; // Run origional function
            }


            // ===== New Harmony Patch
            [HarmonyPatch("AddSubdirectory")]
            [HarmonyPrefix]
            public static bool AddSubdirectory(string name, bool dummyOnly, RecordDirectory __instance, ref RecordDirectory __result)
            {
                // ===== If selected inventory is our local store
                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    // --- Fix the path so it'll actually save, this seems to just be a folder dir thing.
                    string fixedCRPath = __instance.ChildRecordPath.Replace('\\', '/');

                    // --- Remove invalid characters from the new folder name
                    name = StripInvalidCharacters(name);

                    // --- Throw error if new folder name has no characters
                    if (name.Length <= 0)   { throw new Exception("Subdirectory with name '" + name + "' already exists."); }

                    // --- Local Store : Data Path store
                    string recDirLoc   = Path.Combine(REC_PATH, fixedCRPath, name);

                    // --- If the dir already exists, throw toys out of the parm and have a good cry
                    if (Directory.Exists(recDirLoc)) { throw new Exception("Subdirectory with name '" + name + "' already exists."); }

                    // --- Create the directory record as a record 
                    var rec = SkyFrost.Base.RecordHelper.CreateForDirectory<FrooxEngine.Store.Record>(LOCAL_OWNER, __instance.ChildRecordPath, name);

                    // --- If Resonite not trolling us
                    if (!dummyOnly)
                    {
                        // - Create the Record Dir
                        Directory.CreateDirectory(recDirLoc);
                        // - Create the Data Dir
                        Directory.CreateDirectory(Path.Combine(DATA_PATH, fixedCRPath, name));
                        // - Create the Thumb Dir
                        Directory.CreateDirectory(Path.Combine(THUMB_PATH, fixedCRPath, name));
                    }

                    // --- Create proper record dir and add spoon feed it to Resonite 
                    var ret = new RecordDirectory(rec, __instance, __instance.Engine);
                    (AccessTools.Field(typeof(RecordDirectory), "subdirectories").GetValue(__instance) as List<RecordDirectory>).Add(ret);
                    __result = ret;
                    return false; // Don't run origional function
                }

                // ========== Exit if (__instance.OwnerId == LOCAL_OWNER) Check
                return true; // Run origional function
            }

            [HarmonyPatch("AddLinkAsync")]
            [HarmonyPrefix]
            public static bool AddLinkAsync(string name, Uri target, RecordDirectory __instance, ref Task<FrooxEngine.Store.Record> __result)
            {
                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    var fixedPath = __instance.ChildRecordPath.Replace('\\', '/');
                    FrooxEngine.Store.Record record = SkyFrost.Base.RecordHelper.CreateForLink<FrooxEngine.Store.Record>(name, __instance.OwnerId, target.ToString(), null);
                    record.Path = __instance.ChildRecordPath;

                    var recPath = Path.Combine(REC_PATH, fixedPath, name + ".json");
                    using (var fs = File.CreateText(recPath))
                    {
                        new JsonSerializer { Formatting = Formatting.Indented }.Serialize(fs, record);
                    }

                    RecordDirectory item = new RecordDirectory(record, __instance, __instance.Engine);
                    (AccessTools.Field(typeof(RecordDirectory), "subdirectories").GetValue(__instance) as List<RecordDirectory>).Add(item);
                    __result = Task<global::FrooxEngine.Store.Record>.Run(() => record);
                    return false;
                }
                return true;
            }


            // ===== New Harmony Patch
            // ===== Tell Resonite that we cannot do public dirs
            [HarmonyPatch("SetPublicRecursively")]
            [HarmonyPrefix]
            public static bool SetPublicRecursively(RecordDirectory __instance)
            {
                if (__instance.OwnerId == LOCAL_OWNER)
                {
                    throw new Exception("You cannot set public on a local directory.");
                }
                return true;
            }

            [HarmonyPatch("DeleteItem")]
            [HarmonyPrefix]
            public static bool DeleteItem(FrooxEngine.Store.Record record, RecordDirectory __instance, ref bool __result)
            {
                // ===== If dir is not our local store then run original function
                if (!(__instance.OwnerId == LOCAL_OWNER))
                {
                    return true;
                }

                // ===== Get the list of records while removing the record to be deleted
                var test = (AccessTools.Field(typeof(RecordDirectory), "records").GetValue(__instance) as List<global::FrooxEngine.Store.Record>).Remove(record);
                if (test)
                {
                    var asset = new Uri(record.AssetURI);
                    if (asset.Scheme == "lstore")
                    {
                        File.Delete(ResolveLstorePath(asset, DATA_PATH));

                        File.Delete(ResolveLstorePath(asset, REC_PATH));

                        if (record.ThumbnailURI != String.Empty)
                        {
                            File.Delete(ResolveLstorePath(new Uri(record.ThumbnailURI), THUMB_PATH));
                        }
                    }
                }
                __result = test;
                return false;
            }

            [HarmonyPatch("DeleteSubdirectory")]
            [HarmonyPrefix]
            public static bool DeleteSubdirectory(RecordDirectory directory, RecordDirectory __instance)
            {
                if (__instance.OwnerId == LOCAL_OWNER)
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
                    foreach (var subdir in dir.Subdirectories.ToList())
                    {
                        await RecursiveDelete(subdir);
                    }
                    foreach (var rec in dir.Records.ToList())
                    {
                        dir.DeleteItem(rec);
                    }
                    if (dir.DirectoryRecord != null)
                    {
                        Directory.Delete(Path.Combine(REC_PATH, dir.Path.Replace('\\', '/')));
                        Directory.Delete(Path.Combine(DATA_PATH, dir.Path.Replace('\\', '/')));
                        Directory.Delete(Path.Combine(THUMB_PATH, dir.Path.Replace('\\', '/')));
                    }
                }
                if (dir.LinkRecord != null)
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
                string scheme = assetURL.Scheme;

                if (scheme == "lstore")
                {
                    __result = new ValueTask<string>(ResolveLstorePath(assetURL, DATA_PATH));
                    return false;
                }
                else if (scheme == "lstorethumb")
                {
                    __result = new ValueTask<string>(ResolveLstorePath(assetURL, THUMB_PATH));
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(DataTreeConverter), nameof(DataTreeConverter.Load), new Type[] { typeof(string), typeof(string) })]
        class JsonSupportAdding
        {
            public static bool Prefix(string file, string ext, ref DataTreeDictionary __result)
            {
                if (ext == null)
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



        // ================================================================================================================================================================
        // ================================================================================================================================================================
        // ================================================================================================================================================================
        // ================================================================================================================================================================
        // ================================================================================================================================================================
        // ================================================================================================================================================================
        // ================================================================================================================================================================
        // ================================================================================================================================================================
        // ================================================================================================================================================================


        /*
         * World saving stuff, currently completely broken
         * 
         
        [HarmonyPatch(typeof(Userspace))]
        class UserspacePatch
        {
            [HarmonyPatch("SaveWorldTaskIntern")]
            [HarmonyPrefix]
            public static bool SaveWorldTaskIntern(World world, FrooxEngine.Store.Record record, RecordOwnerTransferer transferer, ref Task<FrooxEngine.Store.Record> __result, Userspace __instance)
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



                        // SavedGraph savedGraph = await completionSource.Task.ConfigureAwait(false);
                        var t0 = completionSource.Task; 
                        t0.Wait();
                        SavedGraph savedGraph = t0.Result;

                        SavedGraph graph = savedGraph;

                        //await default(ToBackground);

                        FrooxEngine.Store.Record result;

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
                                    FrooxEngine.Store.Record correspondingRecord = parent.CorrespondingRecord;
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
                            Uri sourceURL = SkyFrost.Base.PlatformProfile.RESONITE.GetRecordUri(record.OwnerId, record.RecordId);


                            if (world.CorrespondingRecord == record)
                            {
                                world.SourceURL = sourceURL;
                            }

                            string worldInfo = string.Format("Name: {0}. RecordId: {1}:{2}. Local: {3}, Global: {4}", new object[] 
                            { 
                                record.Name, 
                                record.OwnerId, 
                                record.RecordId, 
                                record.Version.LocalVersion, 
                                record.Version.GlobalVersion 
                            });

                            string p = Path.Combine(DATA_PATH, record.Path.Replace('\\', '/'), (record.Name + ".json"));

                            //graph2

                            Msg(record.Path);

                            //DataTreeConverter.Save(graph.Root, p + ".json");


                            DataTreeConverter.Save(graph2.Root, p, DataTreeConverter.Compression.LZ4);

                            AccessTools.Method(typeof(DataTreeConverter), "Write", null, null).Invoke(null, new object[] { dataTreeDir, wr });


                            var recPath = Path.Combine(REC_PATH, record.Path.Replace('\\', '/'), record.Name + ".json");
                            using (var fs = File.CreateText(recPath))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                serializer.Formatting = Formatting.Indented;
                                serializer.Serialize(fs, record);
                            }

                            result = record;


                            /*
                            TaskAwaiter<RecordManager.RecordSaveResult> taskAwaiter = base.Engine.RecordManager.SaveRecord(record, graph).GetAwaiter();
                            if (!taskAwaiter.IsCompleted)
                            {
                                await taskAwaiter;
                                TaskAwaiter<RecordManager.RecordSaveResult> taskAwaiter2;
                                taskAwaiter = taskAwaiter2;
                                taskAwaiter2 = default(TaskAwaiter<RecordManager.RecordSaveResult>);
                            }
                            if (!taskAwaiter.GetResult().saved)
                            //


                        }
                        catch (Exception ex)
                        {
                            string tempFilePath = __instance.Engine.LocalDB.GetTempFilePath(".lz4bson");
                            DataTreeConverter.Save(graph.Root, tempFilePath, DataTreeConverter.Compression.LZ4);
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
        /**/
    }
}