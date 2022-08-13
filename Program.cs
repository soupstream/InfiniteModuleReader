using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using OodleSharp;

namespace InfiniteModuleReader
{
    class Program
    {
        const string GameDir = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Halo Infinite";
        static void Main(string[] args)
        {
            string[] files = Directory.GetFiles(Path.Combine(GameDir, "deploy"), "*.module", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                ReadModule(file);
            }
        }

        static void ReadModule(string ModulePath)
        {
            Console.WriteLine(ModulePath);
            //string SearchTerm = "ability_grapple_hook.grapplehookdefinitiontag";
            FileStream fileStream = new FileStream(ModulePath, FileMode.Open);
            byte[] ModuleHeader = new byte[72];
            fileStream.Read(ModuleHeader, 0, 72);
            Module module = new Module
            {
                Head = Encoding.ASCII.GetString(ModuleHeader, 0, 4),
                Version = BitConverter.ToInt32(ModuleHeader, 4),
                ModuleId = BitConverter.ToInt64(ModuleHeader, 8),
                ItemCount = BitConverter.ToInt32(ModuleHeader, 16),
                ManifestCount = BitConverter.ToInt32(ModuleHeader, 20),
                ResourceIndex = BitConverter.ToInt32(ModuleHeader, 32),
                StringsSize = BitConverter.ToInt32(ModuleHeader, 36),
                ResourceCount = BitConverter.ToInt32(ModuleHeader, 40),
                BlockCount = BitConverter.ToInt32(ModuleHeader, 44)
            };
            module.StringTableOffset = module.ItemCount * 88 + 72; //72 is header size
            module.ResourceListOffset = module.StringTableOffset + module.StringsSize + 8; //Still dunno why these 8 bytes are here
            module.BlockListOffset = module.ResourceCount * 4 + module.ResourceListOffset;
            module.FileDataOffset = module.BlockCount * 20 + module.BlockListOffset; //inaccurate, need to skip past a bunch of 00s
            
            int ItemsSize = module.ItemCount * 88;
            byte[] ModuleItems = new byte[ItemsSize];
            fileStream.Read(ModuleItems, 0, ItemsSize);
            fileStream.Seek(8, SeekOrigin.Current); //No idea what these bytes are for
            byte[] ModuleStrings = new byte[module.StringsSize];
            fileStream.Read(ModuleStrings, 0, module.StringsSize);

            //To fix the data offset
            fileStream.Seek(module.FileDataOffset, SeekOrigin.Begin);
            while (fileStream.ReadByte() == 0)
            {
                continue;
            }
            module.FileDataOffset = fileStream.Position - 1;

            Dictionary<int, string> StringList = new Dictionary<int, string>();

            for (int i = 0; i < ItemsSize; i += 88)
            {
                ModuleItem moduleItem = new ModuleItem
                {
                    ResourceCount = BitConverter.ToInt32(ModuleItems, i),
                    ParentIndex = BitConverter.ToInt32(ModuleItems, i + 4), //Seems to always be 0
                    //unknown int16 8
                    BlockCount = BitConverter.ToInt16(ModuleItems, i + 10),
                    BlockIndex = BitConverter.ToInt32(ModuleItems, i + 12),
                    ResourceIndex = BitConverter.ToInt32(ModuleItems, i + 16),
                    ClassId = BitConverter.ToInt32(ModuleItems, i + 20),
                    DataOffset = BitConverter.ToUInt32(ModuleItems, i + 24), //some special stuff needs to be done here, check back later
                    //unknown int16 30
                    TotalCompressedSize = BitConverter.ToUInt32(ModuleItems, i + 32),
                    TotalUncompressedSize = BitConverter.ToUInt32(ModuleItems, i + 36),
                    GlobalTagId = BitConverter.ToInt32(ModuleItems, i + 40),
                    UncompressedHeaderSize = BitConverter.ToUInt32(ModuleItems, i + 44),
                    UncompressedTagDataSize = BitConverter.ToUInt32(ModuleItems, i + 48),
                    UncompressedResourceDataSize = BitConverter.ToUInt32(ModuleItems, i + 52),
                    HeaderBlockCount = BitConverter.ToInt16(ModuleItems, i + 56),
                    TagDataBlockCount = BitConverter.ToInt16(ModuleItems, i + 58),
                    ResourceBlockCount = BitConverter.ToInt16(ModuleItems, i + 60),
                    //padding
                    NameOffset = BitConverter.ToInt32(ModuleItems, i + 64),
                    //unknown int32 68 //Seems to always be -1
                    AssetChecksum = BitConverter.ToInt64(ModuleItems, i + 72),
                    AssetId = BitConverter.ToInt64(ModuleItems, i + 80)
                };
                if (moduleItem.GlobalTagId == -1)
                {
                    continue;
                }
                ModuleItem moduleItemNext = new ModuleItem();
                string TagName = "";
                if (i + 88 != ItemsSize)
                {
                    moduleItemNext.NameOffset = BitConverter.ToInt32(ModuleItems, i + 88 + 64);
                    TagName = Encoding.ASCII.GetString(ModuleStrings, moduleItem.NameOffset, moduleItemNext.NameOffset - moduleItem.NameOffset);
                }
                else
                {
                    TagName = Encoding.ASCII.GetString(ModuleStrings, moduleItem.NameOffset, module.StringsSize - moduleItem.NameOffset);
                }
                StringList.Add(moduleItem.GlobalTagId, TagName);
                //for testing
                if (moduleItem.TotalUncompressedSize == 0)
                {
                    continue;
                }
                long BlockInsertionPoint = 0;
                uint DataCompressedSize = 0;
                ulong FirstBlockOffset = moduleItem.DataOffset + (ulong)module.FileDataOffset;
                string ShortTagName = TagName.Substring(TagName.LastIndexOf("\\") + 1, TagName.Length - TagName.LastIndexOf("\\") - 2);

                if (moduleItem.BlockCount != 0)
                {         
                    for (int y = 0; y < moduleItem.BlockCount; y++)
                    {
                        byte[] BlockBuffer = new byte[20];
                        fileStream.Seek((moduleItem.BlockIndex * 20)  + module.BlockListOffset + (y * 20), 0);
                        //Console.WriteLine("Block Info Location: {0}", fileStream.Position);
                        fileStream.Read(BlockBuffer, 0, 20);
                        Block block = new Block
                        {
                            CompressedOffset = BitConverter.ToUInt32(BlockBuffer, 0),
                            CompressedSize = BitConverter.ToUInt32(BlockBuffer, 4),
                            UncompressedOffset = BitConverter.ToUInt32(BlockBuffer, 8),
                            UncompressedSize = BitConverter.ToUInt32(BlockBuffer, 12),
                            Compressed = BitConverter.ToBoolean(BlockBuffer, 16)
                        };

                        //This is where it gets ugly-er
                        string CurrentBlock = (y == 0) ? "Header" : "Data";
                        byte[] BlockFile = new byte[block.CompressedSize];
                        ulong BlockOffset = FirstBlockOffset + block.CompressedOffset;
                        fileStream.Seek((long)BlockOffset, 0);
                        if (y == 1)
                        {
                            BlockInsertionPoint = fileStream.Position;
                            DataCompressedSize = block.CompressedSize;
                        }
                        //Console.WriteLine("Block Location {1}: {0}, Block Size: {2}", fileStream.Position, y + 1, block.CompressedSize); //Insert block back in here
                        fileStream.Read(BlockFile, 0, (int)block.CompressedSize);
                        if (block.Compressed)
                        {
                            byte[] DecompressedFile = Oodle.Decompress(BlockFile, BlockFile.Length, (int)block.UncompressedSize);
                            if (y == 1) //if block is tag data
                            {
                                //Console.WriteLine();
                            }
                        }
                        else //if the block file is uncompressed
                        {
                        }
                    }
                    //Console.WriteLine("The second block list index will be for the data and where you should reinsert the file when compressed");
                }
                else
                {
                    fileStream.Seek(moduleItem.DataOffset + moduleItem.TotalCompressedSize, 0);
                }

            }

            string relativePath = Path.GetRelativePath(GameDir, ModulePath);
            Directory.CreateDirectory(Path.GetDirectoryName(relativePath));
            StreamWriter output = new StreamWriter(relativePath + ".txt");
            foreach (KeyValuePair<int, string> kvp in StringList)
            {
                output.Write("0x{0:X8} ({0,11}) : {1}\n", kvp.Key, kvp.Value.Trim('\0'));
            }

            //module.PrintInfo();
            fileStream.Close();
            output.Close();
        }
    }
}
