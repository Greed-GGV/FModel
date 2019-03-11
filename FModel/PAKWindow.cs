﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FModel
{
    public partial class PAKWindow : Form
    {
        public static ConfigFile conf;
        private static string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).ToString() + "\\FModel";
        private static string[] PAKFileAsTXT;
        private static string ItemName;
        private static List<string> afterItems;
        public static string[] SelectedArray;

        PrivateFontCollection pfc = new PrivateFontCollection();
        StringFormat centeredString = new StringFormat();
        StringFormat rightString = new StringFormat();
        StringFormat centeredStringLine = new StringFormat();
        private int fontLength;
        private byte[] fontdata;

        public PAKWindow()
        {
            InitializeComponent();
        }

        /*****************************
         USEFUL STUFF FOR THIS PROJECT
         *****************************/
        private void AppendText(string text, Color color, bool addNewLine = false)
        {
            ConsoleRichTextBox.SuspendLayout();
            ConsoleRichTextBox.SelectionColor = color;
            ConsoleRichTextBox.AppendText(addNewLine
                ? $"{text}{Environment.NewLine}"
                : text);
            ConsoleRichTextBox.ScrollToCaret();
            ConsoleRichTextBox.ResumeLayout();
        }
        private static void jwpmProcess(string args)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = docPath + "/john-wick-parse-modded.exe";
                p.StartInfo.Arguments = args;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                p.WaitForExit();
            }
        }
        public static void OpenWithDefaultProgramAndNoFocus(string path)
        {
            Process fileopener = new Process();
            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"" + path + "\"";
            fileopener.Start();
        }

        private void PAKWindow_Load(object sender, EventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.White; //DEFAULT CONSOLE COLOR
            conf = new ConfigFile(); //CREATE CONFIG FILE

            if (!Directory.Exists(Config.conf.pathToFortnitePAKs))
            {
                AppendText("[PathNotFoundException] ", Color.Red);
                AppendText(" Please go to ", Color.Black);
                AppendText(docPath, Color.SteelBlue);
                AppendText(" and enter your Fortnite .PAK files path in ", Color.Black);
                AppendText("config.json", Color.SteelBlue, true);
            }
            else
            {
                IEnumerable<string> yourPAKs = Directory.GetFiles(Config.conf.pathToFortnitePAKs).Where(x => x.EndsWith(".pak"));
                for (int i = 0; i < yourPAKs.Count(); i++)
                {
                    PAKsComboBox.Items.Add(Path.GetFileName(yourPAKs.ElementAt(i)));
                }
            }

            if (!File.Exists("key.txt"))
            {
                AppendText("[FileNotFoundException] ", Color.Red);
                AppendText("File ", Color.Black);
                AppendText("key.txt ", Color.SteelBlue);
                AppendText("created", Color.Black, true);

                File.Create("key.txt").Close();
            }
            AESKeyTextBox.Text = "0x" + File.ReadAllText("key.txt").ToUpper();

            if (!File.Exists(docPath + "\\john-wick-parse-modded.exe"))
            {
                WebClient Client = new WebClient();
                Client.DownloadFile("https://dl.dropbox.com/s/9y5rv3hycin3w8r/john-wick-parse-modded.exe?dl=0", docPath + "\\john-wick-parse-modded.exe");

                AppendText("[FileNotFoundException] ", Color.Red);
                AppendText("File ", Color.Black);
                AppendText("john-wick-parse-modded.exe ", Color.SteelBlue);
                AppendText("downloaded successfully", Color.Black, true);
            }

            ExtractButton.Enabled = false;
            SaveImageButton.Enabled = false;
            SaveImageCheckBox.Enabled = false;

            fontLength = Properties.Resources.BurbankBigCondensed_Bold.Length;
            fontdata = Properties.Resources.BurbankBigCondensed_Bold;
            System.IntPtr weirdData = Marshal.AllocCoTaskMem(fontLength);
            Marshal.Copy(fontdata, 0, weirdData, fontLength);
            pfc.AddMemoryFont(weirdData, fontLength);

            centeredString.Alignment = StringAlignment.Center;
            rightString.Alignment = StringAlignment.Far;
            centeredStringLine.LineAlignment = StringAlignment.Center;
            centeredStringLine.Alignment = StringAlignment.Center;
        } //EVERYTHING TO SET WHEN APP IS STARTING

        private void CreatePath(TreeNodeCollection nodeList, string path)
        {
            TreeNode node = null;
            string folder = string.Empty;
            int p = path.IndexOf('/');

            if (p == -1)
            {
                folder = path;
                path = "";
            }
            else
            {
                folder = path.Substring(0, p);
                path = path.Substring(p + 1, path.Length - (p + 1));
            }

            node = null;
            foreach (TreeNode item in nodeList)
            {
                if (item.Text == folder)
                {
                    node = item;
                }
            }
            if (node == null)
            {
                node = new TreeNode(folder);
                nodeList.Add(node);
            }
            if (path != "")
            {
                CreatePath(node.Nodes, path);
            }
        }
        private static string readPAKGuid(string pakPath)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(pakPath, FileMode.Open)))
            {
                reader.BaseStream.Seek(reader.BaseStream.Length - 61, SeekOrigin.Begin);
                uint g1 = reader.ReadUInt32();
                reader.BaseStream.Seek(reader.BaseStream.Length - 57, SeekOrigin.Begin);
                uint g2 = reader.ReadUInt32();
                reader.BaseStream.Seek(reader.BaseStream.Length - 53, SeekOrigin.Begin);
                uint g3 = reader.ReadUInt32();
                reader.BaseStream.Seek(reader.BaseStream.Length - 49, SeekOrigin.Begin);
                uint g4 = reader.ReadUInt32();

                var guid = g1 + "-" + g2 + "-" + g3 + "-" + g4;
                return guid;
            }
        }

        private static string currentPAK;
        private static string currentGUID;
        private async void PAKsLoad_Click(object sender, EventArgs e)
        {
            PAKTreeView.Nodes.Clear();
            ItemsListBox.Items.Clear();
            File.WriteAllText("key.txt", AESKeyTextBox.Text.Substring(2));

            currentPAK = PAKsComboBox.SelectedItem.ToString();
            PAKsLoad.Enabled = false;
            await Task.Run(() => {
                jwpmProcess("filelist \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + docPath + "\"");
            });
            PAKsLoad.Enabled = true;
            currentGUID = readPAKGuid(Config.conf.pathToFortnitePAKs + "\\" + PAKsComboBox.SelectedItem);

            if (!File.Exists(docPath + "\\" + PAKsComboBox.SelectedItem + ".txt"))
            {
                AppendText("✗ ", Color.Red);
                AppendText(" Can't read ", Color.Black);
                AppendText(PAKsComboBox.SelectedItem.ToString(), Color.SteelBlue);
                AppendText(" with this key", Color.Black, true);
            }
            else
            {
                PAKFileAsTXT = File.ReadAllLines(docPath + "\\" + PAKsComboBox.SelectedItem + ".txt");
                File.Delete(docPath + "\\" + PAKsComboBox.SelectedItem + ".txt");

                for (int i = 0; i < PAKFileAsTXT.Length; i++)
                {
                    CreatePath(PAKTreeView.Nodes, PAKFileAsTXT[i].Replace(PAKFileAsTXT[i].Split('/').Last(), ""));
                }
            }
        }

        public static IEnumerable<TItem> GetAncestors<TItem>(TItem item, Func<TItem, TItem> getParentFunc)
        {
            if (getParentFunc == null)
            {
                throw new ArgumentNullException("getParentFunc");
            }
            if (ReferenceEquals(item, null)) yield break;
            for (TItem curItem = getParentFunc(item); !ReferenceEquals(curItem, null); curItem = getParentFunc(curItem))
            {
                yield return curItem;
            }
        }
        private void PAKTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            List<string> beforeItems = new List<string>();
            afterItems = new List<string>();

            ItemsListBox.Items.Clear();
            FilterTextBox.Text = string.Empty;

            var all = GetAncestors(e.Node, x => x.Parent).ToList();
            all.Reverse();
            var full = string.Join("/", all.Select(x => x.Text)) + "/" + e.Node.Text + "/";
            if (string.IsNullOrEmpty(full))
            {
                return;
            }
            var dircount = full.Count(f => f == '/');
            var dirfiles = PAKFileAsTXT.Where(x => x.StartsWith(full) && !x.Replace(full, "").Contains("/"));
            if (dirfiles.Count() == 0)
            {
                return;
            }

            foreach (var i in dirfiles)
            {
                string v = string.Empty;
                if (i.Contains(".uasset") || i.Contains(".uexp") || i.Contains(".ubulk"))
                {
                    v = i.Substring(0, i.LastIndexOf('.'));
                }
                else
                {
                    v = i.Replace(full, "");
                }
                beforeItems.Add(v.Replace(full, ""));
            }
            afterItems = beforeItems.Distinct().ToList(); //NO DUPLICATION + NO EXTENSION = EASY TO FIND WHAT WE WANT
            for (int i = 0; i < afterItems.Count; i++)
            {
                ItemsListBox.Items.Add(afterItems[i]);
            }

            ExtractButton.Enabled = ItemsListBox.SelectedIndex >= 0;
        }
        private void ItemsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ItemsListBox.SelectedItem != null && SelectedArray == null)
            {
                ExtractButton.Enabled = true;
            }
        } //NO EXTRACT BUTTON IF NOTHING SELECTED

        private static bool CaseInsensitiveContains(string text, string value, StringComparison stringComparison = StringComparison.CurrentCultureIgnoreCase)
        {
            return text.IndexOf(value, stringComparison) >= 0;
        } //FILTER INSENSITIVE
        private void FilterTextBox_TextChanged(object sender, EventArgs e)
        {
            ItemsListBox.BeginUpdate();
            ItemsListBox.Items.Clear();
            if (!string.IsNullOrEmpty(FilterTextBox.Text))
            {
                for (int i = 0; i < afterItems.Count; i++)
                {
                    if (CaseInsensitiveContains(afterItems[i], FilterTextBox.Text))
                    {
                        ItemsListBox.Items.Add(afterItems[i]);
                    }
                }
            }
            else
            {
                for (int i = 0; i < afterItems.Count; i++)
                {
                    ItemsListBox.Items.Add(afterItems[i]);
                }
            }
            ItemsListBox.EndUpdate();
        } //FILTER METHOD

        byte[] oggFind = { 0x4F, 0x67, 0x67, 0x53 };
        byte[] oggNoHeader = { 0x4F, 0x67, 0x67, 0x53 };
        byte[] uexpToDelete = { 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x05, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00 };
        byte[] oggOutNewArray = null;
        static public List<int> SearchBytePattern(byte[] pattern, byte[] bytes)
        {
            List<int> positions = new List<int>();
            int patternLength = pattern.Length;
            int totalLength = bytes.Length;
            byte firstMatchByte = pattern[0];
            for (int i = 0; i < totalLength; i++)
            {
                if (firstMatchByte == bytes[i] && totalLength - i >= patternLength)
                {
                    byte[] match = new byte[patternLength];
                    Array.Copy(bytes, i, match, 0, patternLength);
                    if (match.SequenceEqual<byte>(pattern))
                    {
                        positions.Add(i);
                        i += patternLength - 1;
                    }
                }
            }
            return positions;
        }
        public static bool TryFindAndReplace<T>(T[] source, T[] pattern, T[] replacement, out T[] newArray)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            newArray = null;
            if (pattern.Length > source.Length)
                return false;

            for (var start = 0; start < source.Length - pattern.Length + 1; start += 1)
            {
                var segment = new ArraySegment<T>(source, start, pattern.Length);
                if (Enumerable.SequenceEqual(segment, pattern))
                {
                    newArray = replacement.Concat(source.Skip(start + pattern.Length)).ToArray();
                    return true;
                }
            }
            return false;
        }
        private void convertToOGG(string file, string item)
        {
            var isUBULKFound = new DirectoryInfo(System.IO.Path.GetDirectoryName(file)).GetFiles(Path.GetFileNameWithoutExtension(file) + "*.ubulk", SearchOption.AllDirectories).FirstOrDefault();
            if (isUBULKFound == null)
            {
                // Handle the file not being found
                AppendText("✔ ", Color.Green);
                AppendText(item, Color.DarkRed);
                AppendText(" has no ", Color.Black);
                AppendText("UBULK file", Color.SteelBlue, true);

                string oggPattern = "OggS";
                if (File.ReadAllText(file).Contains(oggPattern))
                {
                    byte[] src = File.ReadAllBytes(file);
                    TryFindAndReplace<byte>(src, oggFind, oggNoHeader, out oggOutNewArray);
                    File.WriteAllBytes(Path.GetFileNameWithoutExtension(file) + ".temp", oggOutNewArray);

                    FileInfo fi = new FileInfo(Path.GetFileNameWithoutExtension(file) + ".temp");
                    FileStream fs = fi.Open(FileMode.Open);
                    long bytesToDelete = 4;
                    fs.SetLength(Math.Max(0, fi.Length - bytesToDelete));
                    fs.Close();

                    byte[] srcFinal = File.ReadAllBytes(Path.GetFileNameWithoutExtension(file) + ".temp");
                    int i = srcFinal.Length - 7;
                    while (srcFinal[i] == 0)
                        --i;
                    byte[] bar = new byte[i + 1];
                    Array.Copy(srcFinal, bar, i + 1);
                    AppendText("✔ ", Color.Green);
                    AppendText("Empty bytes", Color.DarkRed);
                    AppendText(" deleted successfully", Color.Black, true);

                    File.WriteAllBytes(docPath + "\\Extracted Sounds\\" + Path.GetFileNameWithoutExtension(file) + ".ogg", bar);
                    File.Delete(Path.GetFileNameWithoutExtension(file) + ".temp");
                }
                else
                {
                    AppendText("✗ ", Color.Red);
                    AppendText("No Sound Pattern Found", Color.Black, true);
                }
            }
            else
            {
                // The file variable has the *first* occurrence of that filename
                AppendText("✔ ", Color.Green);
                AppendText(item, Color.DarkRed);
                AppendText(" extracted with an ", Color.Black);
                AppendText("UBULK file", Color.SteelBlue, true);

                string oggPattern = "OggS";
                if (File.ReadAllText(file).Contains(oggPattern))
                {
                    byte[] src = File.ReadAllBytes(file);
                    List<int> positions = SearchBytePattern(uexpToDelete, src);

                    AppendText("UBULK Footer Index: ", Color.Black);
                    AppendText(positions[0].ToString("X2"), Color.Green, true);
                    AppendText("Source Last Index: ", Color.Black);
                    AppendText(src.Length.ToString("X2"), Color.Green, true);

                    TryFindAndReplace<byte>(src, oggFind, oggNoHeader, out oggOutNewArray);
                    File.WriteAllBytes(Path.GetFileNameWithoutExtension(file) + ".temp", oggOutNewArray);

                    int lengthToDelete = src.Length - positions[0];

                    FileInfo fi = new FileInfo(Path.GetFileNameWithoutExtension(file) + ".temp");
                    FileStream fs = fi.Open(FileMode.Open);
                    long bytesToDelete = lengthToDelete;
                    fs.SetLength(Math.Max(0, fi.Length - bytesToDelete));
                    fs.Close();

                    byte[] src44 = File.ReadAllBytes(Path.GetFileNameWithoutExtension(file) + ".temp");
                    byte[] srcUBULK = File.ReadAllBytes(Path.GetDirectoryName(file) + "\\" + isUBULKFound.ToString());
                    byte[] buffer = new byte[srcUBULK.Length];
                    using (FileStream fs1 = new FileStream(Path.GetDirectoryName(file) + "\\" + isUBULKFound.ToString(), FileMode.Open, FileAccess.ReadWrite))
                    {
                        AppendText("✔ ", Color.Green);
                        AppendText("Writing ", Color.Black);
                        AppendText("UBULK Data", Color.DarkRed, true);

                        fs1.Read(buffer, 0, buffer.Length);

                        FileStream fs2 = new FileStream(Path.GetFileNameWithoutExtension(file) + ".temp", FileMode.Open, FileAccess.ReadWrite);
                        fs2.Position = src44.Length;
                        fs2.Write(buffer, 0, buffer.Length);
                        fs2.Close();
                        fs1.Close();
                    }

                    byte[] srcFinal = File.ReadAllBytes(Path.GetFileNameWithoutExtension(file) + ".temp");
                    int i = srcFinal.Length - 1;
                    while (srcFinal[i] == 0)
                        --i;
                    byte[] bar = new byte[i + 1];
                    Array.Copy(srcFinal, bar, i + 1);
                    AppendText("✔ ", Color.Green);
                    AppendText("Empty bytes", Color.DarkRed);
                    AppendText(" deleted successfully", Color.Black, true);

                    File.WriteAllBytes(docPath + "\\Extracted Sounds\\" + Path.GetFileNameWithoutExtension(file) + ".ogg", bar);
                    File.Delete(Path.GetFileNameWithoutExtension(file) + ".temp");
                }
                else
                {
                    AppendText("✗ ", Color.Red);
                    AppendText("No Sound Pattern Found", Color.Black, true);
                }
            }
        }

        private void convertToOTF(string file, string item)
        {
            AppendText("✔ ", Color.Green);
            AppendText(item, Color.DarkRed);
            AppendText(" is a ", Color.Black);
            AppendText("font", Color.SteelBlue, true);

            File.Move(file, Path.ChangeExtension(file, ".otf"));

            AppendText("✔ ", Color.Green);
            AppendText(item, Color.DarkRed);
            AppendText(" successfully converter to a ", Color.Black);
            AppendText("font", Color.SteelBlue, true);
        }

        private void getItemRarity(ItemsIdParser parser, Graphics toDrawOn)
        {
            if (parser.Rarity == "EFortRarity::Legendary")
            {
                Image RarityBG = Properties.Resources.I512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("IMPOSSIBLE (T9)", Color.DarkOrange, true);
            }
            if (parser.Rarity == "EFortRarity::Masterwork")
            {
                Image RarityBG = Properties.Resources.T512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("TRANSCENDENT", Color.OrangeRed, true);
            }
            if (parser.Rarity == "EFortRarity::Elegant")
            {
                Image RarityBG = Properties.Resources.M512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("MYTHIC", Color.Yellow, true);
            }
            if (parser.Rarity == "EFortRarity::Fine")
            {
                Image RarityBG = Properties.Resources.L512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("LEGENDARY", Color.Orange, true);
            }
            if (parser.Rarity == "EFortRarity::Quality")
            {
                Image RarityBG = Properties.Resources.E512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("EPIC", Color.Purple, true);
            }
            if (parser.Rarity == "EFortRarity::Sturdy")
            {
                Image RarityBG = Properties.Resources.R512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("RARE", Color.Blue, true);
            }
            if (parser.Rarity == "EFortRarity::Handmade")
            {
                Image RarityBG = Properties.Resources.C512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("COMMON", Color.DarkGray, true);
            }
            if (parser.Rarity == null)
            {
                Image RarityBG = Properties.Resources.U512;
                toDrawOn.DrawImage(RarityBG, new Point(0, 0));
                AppendText("Item Rarity: ", Color.Black);
                AppendText("UNCOMMON", Color.Green, true);
            }
        }

        private async void ExtractButton_Click(object sender, EventArgs e)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            ItemRichTextBox.Text = "";
            ItemIconPictureBox.Image = null;

            if (!Directory.Exists(docPath + "\\Extracted\\")) //Create Extracted Subfolder
                Directory.CreateDirectory(docPath + "\\Extracted\\");
            if (!Directory.Exists(docPath + "\\Generated Icons\\")) //Create Generated Icons Subfolder
                Directory.CreateDirectory(docPath + "\\Generated Icons\\");
            if (!Directory.Exists(docPath + "\\Extracted Sounds\\")) //Create Generated Icons Subfolder
                Directory.CreateDirectory(docPath + "\\Extracted Sounds\\");

            SelectedArray = new string[ItemsListBox.SelectedItems.Count];
            for (int i = 0; i < ItemsListBox.SelectedItems.Count; i++) //ADD SELECTED ITEM TO ARRAY
            {
                SelectedArray[i] = ItemsListBox.SelectedItems[i].ToString();
            }

            ExtractButton.Enabled = false;
            for (int i = 0; i < SelectedArray.Length; i++)
            {
                string currentItem = SelectedArray[i].ToString();

                var files = Directory.GetFiles(docPath + "\\Extracted", currentItem + ".*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".png")).FirstOrDefault();
                if (!File.Exists(files))
                {
                    await Task.Run(() => {
                        jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + currentItem + "\" \"" + docPath + "\"");
                    });
                    files = Directory.GetFiles(docPath + "\\Extracted", currentItem + ".*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".png")).FirstOrDefault();
                }
                if (files != null)
                {
                    AppendText("", Color.Black, true);
                    AppendText("✔ ", Color.Green);
                    AppendText(currentItem, Color.DarkRed);
                    AppendText(" successfully extracted to ", Color.Black);
                    AppendText(files.Substring(0, files.LastIndexOf('.')), Color.SteelBlue, true);

                    if (files.Contains(".uasset") || files.Contains(".uexp") || files.Contains(".ubulk"))
                    {
                        AppendText("✔ ", Color.Green);
                        AppendText(currentItem, Color.DarkRed);
                        AppendText(" is an ", Color.Black);
                        AppendText("asset", Color.SteelBlue, true);

                        await Task.Run(() => {
                            jwpmProcess("serialize \"" + files.Substring(0, files.LastIndexOf('.')) + "\"");
                        });
                        var filesJSON = Directory.GetFiles(docPath, currentItem + ".json", SearchOption.AllDirectories).FirstOrDefault();
                        if (filesJSON != null)
                        {
                            var json = JToken.Parse(File.ReadAllText(filesJSON)).ToString();
                            File.Delete(filesJSON);
                            AppendText("✔ ", Color.Green);
                            AppendText(currentItem, Color.DarkRed);
                            AppendText(" successfully serialized", Color.Black, true);
                            ItemRichTextBox.Text = json;

                            var IDParser = ItemsIdParser.FromJson(json);

                            if (LoadDataCheckBox.Checked == true)
                            {
                                AppendText("Auto loading data set to ", Color.Black);
                                AppendText("True", Color.Green, true);

                                if (filesJSON.Contains("Athena\\Items\\Cosmetics")) //ASSET IS AN ID => CREATE ICON
                                {
                                    AppendText("✔ ", Color.Green);
                                    AppendText(currentItem, Color.DarkRed);
                                    AppendText(" is an ", Color.Black);
                                    AppendText("ID file", Color.SteelBlue, true);
                                    AppendText("Parsing...", Color.Black, true);

                                    for (int iii = 0; iii < IDParser.Length; iii++)
                                    {
                                        if (IDParser[iii].ExportType.Contains("Item") && IDParser[iii].ExportType.Contains("Definition"))
                                        {
                                            ItemName = IDParser[iii].DisplayName;
                                            Bitmap bmp = new Bitmap(522, 522);
                                            Graphics g = Graphics.FromImage(bmp);

                                            getItemRarity(IDParser[iii], g);

                                            string itemIconPath = string.Empty;

                                            if (IDParser[iii].HeroDefinition != null)
                                            {
                                                var filesPath = Directory.GetFiles(docPath + "\\Extracted", IDParser[iii].HeroDefinition + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                if (!File.Exists(filesPath))
                                                {
                                                    AppendText("✔ ", Color.Green);
                                                    AppendText("Extracting ", Color.Black);
                                                    AppendText(IDParser[iii].HeroDefinition, Color.DarkRed, true);

                                                    await Task.Run(() => {
                                                        jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + IDParser[iii].HeroDefinition + "\" \"" + docPath + "\"");
                                                    });
                                                    filesPath = Directory.GetFiles(docPath + "\\Extracted", IDParser[iii].HeroDefinition + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                }
                                                try
                                                {
                                                    if (filesPath != null)
                                                    {
                                                        AppendText("✔ ", Color.Green);
                                                        AppendText(IDParser[iii].HeroDefinition, Color.DarkRed);
                                                        AppendText(" successfully extracted to ", Color.Black);
                                                        AppendText(filesPath.Substring(0, filesPath.LastIndexOf('.')), Color.SteelBlue, true);
                                                        try
                                                        {
                                                            await Task.Run(() => {
                                                                jwpmProcess("serialize \"" + filesPath.Substring(0, filesPath.LastIndexOf('.')) + "\"");
                                                            });
                                                            var filesJSON2 = Directory.GetFiles(docPath, IDParser[iii].HeroDefinition + ".json", SearchOption.AllDirectories).FirstOrDefault();
                                                            var json2 = JToken.Parse(File.ReadAllText(filesJSON2)).ToString();
                                                            File.Delete(filesJSON2);
                                                            AppendText("✔ ", Color.Green);
                                                            AppendText(IDParser[iii].HeroDefinition, Color.DarkRed);
                                                            AppendText(" successfully serialized", Color.Black, true);

                                                            var IDParser2 = ItemsIdParser.FromJson(json2);
                                                            for (int i1 = 0; i1 < IDParser2.Length; i1++)
                                                            {
                                                                if (IDParser2[i1].LargePreviewImage != null)
                                                                {
                                                                    string textureFile = Path.GetFileName(IDParser2[i1].LargePreviewImage.AssetPathName).Substring(0, Path.GetFileName(IDParser2[i1].LargePreviewImage.AssetPathName).LastIndexOf('.'));
                                                                    AppendText("✔ ", Color.Green);
                                                                    AppendText(textureFile, Color.DarkRed);
                                                                    AppendText(" detected as a ", Color.Black);
                                                                    AppendText("Texture2D file", Color.SteelBlue, true);

                                                                    var filesPath2 = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                                    if (!File.Exists(filesPath2))
                                                                    {
                                                                        if (currentGUID != "0-0-0-0")
                                                                        {
                                                                            await Task.Run(() => {
                                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                                            });
                                                                            filesPath2 = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                                        }
                                                                        else
                                                                        {
                                                                            await Task.Run(() => {
                                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\pakchunk0_s7-WindowsClient.pak" + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                                            });
                                                                            filesPath2 = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                                        }
                                                                    }
                                                                    try
                                                                    {
                                                                        if (filesPath2 != null)
                                                                        {
                                                                            AppendText("✔ ", Color.Green);
                                                                            AppendText(textureFile, Color.DarkRed);
                                                                            AppendText(" successfully extracted to ", Color.Black);
                                                                            AppendText(filesPath2.Substring(0, filesPath2.LastIndexOf('.')), Color.SteelBlue, true);

                                                                            itemIconPath = filesPath2.Substring(0, filesPath2.LastIndexOf('.')) + ".png";
                                                                            if (!File.Exists(itemIconPath))
                                                                            {
                                                                                await Task.Run(() => {
                                                                                    jwpmProcess("texture \"" + filesPath2.Substring(0, filesPath2.LastIndexOf('.')) + "\"");
                                                                                });
                                                                                itemIconPath = filesPath2.Substring(0, filesPath2.LastIndexOf('.')) + ".png";
                                                                            }

                                                                            AppendText("✔ ", Color.Green);
                                                                            AppendText(textureFile, Color.DarkRed);
                                                                            AppendText(" successfully converted to a PNG image with path ", Color.Black);
                                                                            AppendText(itemIconPath, Color.SteelBlue, true);
                                                                        }
                                                                    }
                                                                    catch (IndexOutOfRangeException)
                                                                    {
                                                                        AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                                        AppendText("Can't extract ", Color.Black);
                                                                        AppendText(textureFile, Color.SteelBlue);
                                                                        AppendText(" in ", Color.Black);
                                                                        AppendText("pakchunk0_s7-WindowsClient.pak", Color.DarkRed, true);
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            Console.WriteLine(ex.Message);
                                                        }
                                                    }
                                                }
                                                catch (IndexOutOfRangeException)
                                                {
                                                    AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                    AppendText("Can't extract ", Color.Black);
                                                    AppendText(IDParser[iii].HeroDefinition, Color.SteelBlue);
                                                    AppendText(" in ", Color.Black);
                                                    AppendText(PAKsComboBox.SelectedItem.ToString(), Color.DarkRed, true);
                                                }
                                            }
                                            else if (IDParser[iii].WeaponDefinition != null)
                                            {
                                                var filesPath = Directory.GetFiles(docPath + "\\Extracted", IDParser[iii].WeaponDefinition + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                if (!File.Exists(filesPath))
                                                {
                                                    AppendText("✔ ", Color.Green);
                                                    AppendText("Extracting ", Color.Black);
                                                    AppendText(IDParser[iii].WeaponDefinition, Color.DarkRed, true);

                                                    await Task.Run(() => {
                                                        jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + IDParser[iii].WeaponDefinition + "\" \"" + docPath + "\"");
                                                    });
                                                    filesPath = Directory.GetFiles(docPath + "\\Extracted", IDParser[iii].WeaponDefinition + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                }
                                                try
                                                {
                                                    if (filesPath != null)
                                                    {
                                                        AppendText("✔ ", Color.Green);
                                                        AppendText(IDParser[iii].WeaponDefinition, Color.DarkRed);
                                                        AppendText(" successfully extracted to ", Color.Black);
                                                        AppendText(filesPath.Substring(0, filesPath.LastIndexOf('.')), Color.SteelBlue, true);
                                                        try
                                                        {
                                                            await Task.Run(() => {
                                                                jwpmProcess("serialize \"" + filesPath.Substring(0, filesPath.LastIndexOf('.')) + "\"");
                                                            });
                                                            var filesJSON2 = Directory.GetFiles(docPath, IDParser[iii].WeaponDefinition + ".json", SearchOption.AllDirectories).FirstOrDefault();
                                                            var json2 = JToken.Parse(File.ReadAllText(filesJSON2)).ToString();
                                                            File.Delete(filesJSON2);
                                                            AppendText("✔ ", Color.Green);
                                                            AppendText(IDParser[iii].WeaponDefinition, Color.DarkRed);
                                                            AppendText(" successfully serialized", Color.Black, true);

                                                            var IDParser2 = ItemsIdParser.FromJson(json2);
                                                            for (int i2 = 0; i2 < IDParser2.Length; i2++)
                                                            {
                                                                if (IDParser2[i2].LargePreviewImage != null)
                                                                {
                                                                    string textureFile = Path.GetFileName(IDParser2[i2].LargePreviewImage.AssetPathName).Substring(0, Path.GetFileName(IDParser2[i2].LargePreviewImage.AssetPathName).LastIndexOf('.'));
                                                                    AppendText("✔ ", Color.Green);
                                                                    AppendText(textureFile, Color.DarkRed);
                                                                    AppendText(" detected as a ", Color.Black);
                                                                    AppendText("Texture2D file", Color.SteelBlue, true);

                                                                    var filesPath2 = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                                    if (!File.Exists(filesPath2))
                                                                    {
                                                                        if (currentGUID != "0-0-0-0")
                                                                        {
                                                                            await Task.Run(() => {
                                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                                            });
                                                                            filesPath2 = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                                        }
                                                                        else
                                                                        {
                                                                            await Task.Run(() => {
                                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\pakchunk0_s7-WindowsClient.pak" + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                                            });
                                                                            filesPath2 = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                                        }
                                                                    }
                                                                    try
                                                                    {
                                                                        if (filesPath2 != null)
                                                                        {
                                                                            AppendText("✔ ", Color.Green);
                                                                            AppendText(textureFile, Color.DarkRed);
                                                                            AppendText(" successfully extracted to ", Color.Black);
                                                                            AppendText(filesPath2.Substring(0, filesPath2.LastIndexOf('.')), Color.SteelBlue, true);

                                                                            itemIconPath = filesPath2.Substring(0, filesPath2.LastIndexOf('.')) + ".png";
                                                                            if (!File.Exists(itemIconPath))
                                                                            {
                                                                                await Task.Run(() => {
                                                                                    jwpmProcess("texture \"" + filesPath2.Substring(0, filesPath2.LastIndexOf('.')) + "\"");
                                                                                });
                                                                                itemIconPath = filesPath2.Substring(0, filesPath2.LastIndexOf('.')) + ".png";
                                                                            }

                                                                            AppendText("✔ ", Color.Green);
                                                                            AppendText(textureFile, Color.DarkRed);
                                                                            AppendText(" successfully converted to a PNG image with path ", Color.Black);
                                                                            AppendText(itemIconPath, Color.SteelBlue, true);
                                                                        }
                                                                    }
                                                                    catch (IndexOutOfRangeException)
                                                                    {
                                                                        AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                                        AppendText("Can't extract ", Color.Black);
                                                                        AppendText(textureFile, Color.SteelBlue);
                                                                        AppendText(" in ", Color.Black);
                                                                        AppendText("pakchunk0_s7-WindowsClient.pak", Color.DarkRed, true);
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            Console.WriteLine(ex.Message);
                                                        }
                                                    }
                                                }
                                                catch (IndexOutOfRangeException)
                                                {
                                                    AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                    AppendText("Can't extract ", Color.Black);
                                                    AppendText(IDParser[iii].WeaponDefinition, Color.SteelBlue);
                                                    AppendText(" in ", Color.Black);
                                                    AppendText(PAKsComboBox.SelectedItem.ToString(), Color.DarkRed, true);
                                                }
                                            }
                                            else if (IDParser[iii].LargePreviewImage != null)
                                            {
                                                string textureFile = Path.GetFileName(IDParser[iii].LargePreviewImage.AssetPathName).Substring(0, Path.GetFileName(IDParser[iii].LargePreviewImage.AssetPathName).LastIndexOf('.'));
                                                AppendText("✔ ", Color.Green);
                                                AppendText(textureFile, Color.DarkRed);
                                                AppendText(" detected as a ", Color.Black);
                                                AppendText("Texture2D file", Color.SteelBlue, true);

                                                var filesPath = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                if (!File.Exists(filesPath))
                                                {
                                                    if (currentGUID != "0-0-0-0") //DYNAMIC PAK
                                                    {
                                                        await Task.Run(() => {
                                                            jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                        });
                                                        filesPath = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                    }
                                                    else //NORMAL PAK
                                                    {
                                                        await Task.Run(() => {
                                                            if (IDParser[iii].LargePreviewImage.AssetPathName.Contains("/Game/2dAssets/"))
                                                            {
                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\pakchunk0-WindowsClient.pak" + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                            }
                                                            else if (IDParser[iii].LargePreviewImage.AssetPathName.Contains("/Game/Athena/TestAssets/") || IDParser[iii].LargePreviewImage.AssetPathName.Contains("/Game/Athena/Prototype/"))
                                                            {
                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                            }
                                                            else
                                                            {
                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\pakchunk0_s7-WindowsClient.pak" + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                            }
                                                        });
                                                        filesPath = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                    }
                                                }
                                                try
                                                {
                                                    if (filesPath != null)
                                                    {
                                                        AppendText("✔ ", Color.Green);
                                                        AppendText(textureFile, Color.DarkRed);
                                                        AppendText(" successfully extracted to ", Color.Black);
                                                        AppendText(filesPath.Substring(0, filesPath.LastIndexOf('.')), Color.SteelBlue, true);

                                                        itemIconPath = filesPath.Substring(0, filesPath.LastIndexOf('.')) + ".png";
                                                        if (!File.Exists(itemIconPath))
                                                        {
                                                            await Task.Run(() => {
                                                                jwpmProcess("texture \"" + filesPath.Substring(0, filesPath.LastIndexOf('.')) + "\"");
                                                            });
                                                            itemIconPath = filesPath.Substring(0, filesPath.LastIndexOf('.')) + ".png";
                                                        }

                                                        AppendText("✔ ", Color.Green);
                                                        AppendText(textureFile, Color.DarkRed);
                                                        AppendText(" successfully converted to a PNG image with path ", Color.Black);
                                                        AppendText(itemIconPath, Color.SteelBlue, true);
                                                    }
                                                }
                                                catch (IndexOutOfRangeException)
                                                {
                                                    AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                    AppendText("Can't extract ", Color.Black);
                                                    AppendText(textureFile, Color.SteelBlue);
                                                    AppendText(" in ", Color.Black);
                                                    AppendText("pakchunk0_s7-WindowsClient.pak", Color.DarkRed, true);
                                                }
                                            }
                                            else if (IDParser[iii].SmallPreviewImage != null)
                                            {
                                                string textureFile = Path.GetFileName(IDParser[iii].SmallPreviewImage.AssetPathName).Substring(0, Path.GetFileName(IDParser[iii].SmallPreviewImage.AssetPathName).LastIndexOf('.'));
                                                AppendText("✔ ", Color.Green);
                                                AppendText(textureFile, Color.DarkRed);
                                                AppendText(" detected as a ", Color.Black);
                                                AppendText("Texture2D file", Color.SteelBlue, true);

                                                var filesPath = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                if (!File.Exists(filesPath))
                                                {
                                                    if (currentGUID != "0-0-0-0")
                                                    {
                                                        await Task.Run(() => {
                                                            jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                        });
                                                        filesPath = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                    }
                                                    else
                                                    {
                                                        await Task.Run(() => {
                                                            if (IDParser[iii].SmallPreviewImage.AssetPathName.Contains("/Game/2dAssets/"))
                                                            {
                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\pakchunk0-WindowsClient.pak" + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                            }
                                                            else if (IDParser[iii].SmallPreviewImage.AssetPathName.Contains("/Game/Athena/TestAssets/") || IDParser[iii].SmallPreviewImage.AssetPathName.Contains("/Game/Athena/Prototype/"))
                                                            {
                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                            }
                                                            else
                                                            {
                                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\pakchunk0_s7-WindowsClient.pak" + "\" \"" + textureFile + "\" \"" + docPath + "\"");
                                                            }
                                                        });
                                                        filesPath = Directory.GetFiles(docPath + "\\Extracted", textureFile + ".*", SearchOption.AllDirectories).FirstOrDefault();
                                                    }
                                                }
                                                try
                                                {
                                                    if (filesPath != null)
                                                    {
                                                        AppendText("✔ ", Color.Green);
                                                        AppendText(textureFile, Color.DarkRed);
                                                        AppendText(" successfully extracted to ", Color.Black);
                                                        AppendText(filesPath.Substring(0, filesPath.LastIndexOf('.')), Color.SteelBlue, true);

                                                        itemIconPath = filesPath.Substring(0, filesPath.LastIndexOf('.')) + ".png";
                                                        if (!File.Exists(itemIconPath))
                                                        {
                                                            await Task.Run(() => {
                                                                jwpmProcess("texture \"" + filesPath.Substring(0, filesPath.LastIndexOf('.')) + "\"");
                                                            });
                                                            itemIconPath = filesPath.Substring(0, filesPath.LastIndexOf('.')) + ".png";
                                                        }

                                                        AppendText("✔ ", Color.Green);
                                                        AppendText(textureFile, Color.DarkRed);
                                                        AppendText(" successfully converted to a PNG image with path ", Color.Black);
                                                        AppendText(itemIconPath, Color.SteelBlue, true);
                                                    }
                                                }
                                                catch (IndexOutOfRangeException)
                                                {
                                                    AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                    AppendText("Can't extract ", Color.Black);
                                                    AppendText(textureFile, Color.SteelBlue);
                                                    AppendText(" in ", Color.Black);
                                                    AppendText("pakchunk0_s7-WindowsClient.pak", Color.DarkRed, true);
                                                }
                                            }

                                            if (File.Exists(itemIconPath))
                                            {
                                                Image ItemIcon = Image.FromFile(itemIconPath);
                                                g.DrawImage(ItemIcon, new Point(5, 5));
                                            }
                                            else
                                            {
                                                Image ItemIcon = Properties.Resources.unknown512;
                                                g.DrawImage(ItemIcon, new Point(0, 0));
                                            }

                                            Image bg512 = Properties.Resources.BG512;
                                            g.DrawImage(bg512, new Point(5, 383));

                                            try
                                            {
                                                g.DrawString(ItemName, new Font(pfc.Families[0], 40), new SolidBrush(Color.White), new Point(522 / 2, 390), centeredString);
                                            }
                                            catch (NullReferenceException)
                                            {
                                                AppendText("[NullReferenceException] ", Color.Red);
                                                AppendText("No ", Color.Black);
                                                AppendText("DisplayName ", Color.SteelBlue);
                                                AppendText("found", Color.Black, true);
                                            } //NAME
                                            try
                                            {
                                                g.DrawString(IDParser[iii].Description, new Font("Arial", 11), new SolidBrush(Color.White), new Point(522 / 2, 465), centeredStringLine);
                                            }
                                            catch (NullReferenceException)
                                            {
                                                AppendText("[NullReferenceException] ", Color.Red);
                                                AppendText("No ", Color.Black);
                                                AppendText("Description ", Color.SteelBlue);
                                                AppendText("found", Color.Black, true);
                                            } //DESCRIPTION
                                            try
                                            {
                                                g.DrawString(IDParser[iii].ShortDescription, new Font(pfc.Families[0], 15), new SolidBrush(Color.White), new Point(5, 498));
                                            }
                                            catch (NullReferenceException)
                                            {
                                                AppendText("[NullReferenceException] ", Color.Red);
                                                AppendText("No ", Color.Black);
                                                AppendText("ShortDescription ", Color.SteelBlue);
                                                AppendText("found", Color.Black, true);
                                            } //TYPE
                                            try
                                            {
                                                g.DrawString(IDParser[iii].GameplayTags.GameplayTagsGameplayTags[Array.FindIndex(IDParser[iii].GameplayTags.GameplayTagsGameplayTags, x => x.StartsWith("Cosmetics.Source."))].Substring(17), new Font(pfc.Families[0], 15), new SolidBrush(Color.White), new Point(522 - 5, 498), rightString);
                                            }
                                            catch (NullReferenceException)
                                            {
                                                AppendText("[NullReferenceException] ", Color.Red);
                                                AppendText("No ", Color.Black);
                                                AppendText("GameplayTags ", Color.SteelBlue);
                                                AppendText("found", Color.Black, true);
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                                AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                AppendText("No ", Color.Black);
                                                AppendText("GameplayTags as Cosmetics.Source ", Color.SteelBlue);
                                                AppendText("found", Color.Black, true);
                                            } //COSMETIC SOURCE

                                            ItemIconPictureBox.Image = bmp;
                                            if (SaveImageCheckBox.Checked == true)
                                            {
                                                AppendText("Auto saving icon set to ", Color.Black);
                                                AppendText("True", Color.Green, true);
                                                ItemIconPictureBox.Image.Save(docPath + "\\Generated Icons\\" + ItemName + ".png", ImageFormat.Png);

                                                AppendText("✔ ", Color.Green);
                                                AppendText(ItemName, Color.DarkRed);
                                                AppendText(" successfully saved to ", Color.Black);
                                                AppendText(docPath + "\\Generated Icons\\" + ItemName + ".png", Color.SteelBlue, true);
                                            }
                                        }
                                    }
                                }
                                for (int ii = 0; ii < IDParser.Length; ii++)
                                {
                                    if (IDParser[ii].ExportType == "Texture2D")
                                    {
                                        AppendText("Parsing...", Color.Black, true);
                                        ItemName = currentItem;

                                        AppendText("✔ ", Color.Green);
                                        AppendText(currentItem, Color.DarkRed);
                                        AppendText(" detected as a ", Color.Black);
                                        AppendText("Texture2D file", Color.SteelBlue, true);

                                        string IMGPath = string.Empty;

                                        var filesPath = Directory.GetFiles(docPath + "\\Extracted", currentItem + ".*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".png")).FirstOrDefault();
                                        if (!File.Exists(filesPath))
                                        {
                                            await Task.Run(() => {
                                                jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + currentItem + "\" \"" + docPath + "\"");
                                            });
                                            filesPath = Directory.GetFiles(docPath + "\\Extracted", currentItem + ".*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".png")).FirstOrDefault();
                                        }
                                        try
                                        {
                                            if (filesPath != null)
                                            {
                                                AppendText("✔ ", Color.Green);
                                                AppendText(currentItem, Color.DarkRed);
                                                AppendText(" successfully extracted to ", Color.Black);
                                                AppendText(filesPath.Substring(0, filesPath.LastIndexOf('.')), Color.SteelBlue, true);

                                                IMGPath = filesPath.Substring(0, filesPath.LastIndexOf('.')) + ".png";
                                                if (!File.Exists(IMGPath))
                                                {
                                                    await Task.Run(() => {
                                                        jwpmProcess("texture \"" + filesPath.Substring(0, filesPath.LastIndexOf('.')) + "\"");
                                                    });
                                                    IMGPath = filesPath.Substring(0, filesPath.LastIndexOf('.')) + ".png";
                                                }

                                                AppendText("✔ ", Color.Green);
                                                AppendText(currentItem, Color.DarkRed);
                                                AppendText(" successfully converted to a PNG image with path ", Color.Black);
                                                AppendText(IMGPath, Color.SteelBlue, true);
                                            }
                                        }
                                        catch (IndexOutOfRangeException)
                                        {
                                            AppendText("[IndexOutOfRangeException] ", Color.Red);
                                            AppendText("Can't extract ", Color.Black);
                                            AppendText(currentItem, Color.SteelBlue);
                                            AppendText(" in ", Color.Black);
                                            AppendText(PAKsComboBox.SelectedItem.ToString(), Color.DarkRed, true);
                                        }

                                        if (File.Exists(IMGPath))
                                        {
                                            ItemIconPictureBox.Image = Image.FromFile(IMGPath);
                                        }
                                        else
                                        {
                                            ItemIconPictureBox.Image = Properties.Resources.unknown512;
                                        }

                                        if (SaveImageCheckBox.Checked == true)
                                        {
                                            AppendText("Auto saving icon set to ", Color.Black);
                                            AppendText("True", Color.Green, true);
                                            ItemIconPictureBox.Image.Save(docPath + "\\Generated Icons\\" + ItemName + ".png", ImageFormat.Png);

                                            AppendText("✔ ", Color.Green);
                                            AppendText(ItemName, Color.DarkRed);
                                            AppendText(" successfully saved to ", Color.Black);
                                            AppendText(docPath + "\\Generated Icons\\" + ItemName + ".png", Color.SteelBlue, true);
                                        }
                                    } //ASSET IS A TEXTURE => LOAD TEXTURE
                                    if (IDParser[ii].ExportType == "SoundWave")
                                    {
                                        AppendText("Parsing...", Color.Black, true);
                                        ItemName = currentItem;

                                        AppendText("✔ ", Color.Green);
                                        AppendText(currentItem, Color.DarkRed);
                                        AppendText(" detected as a ", Color.Black);
                                        AppendText("SoundWave file", Color.SteelBlue, true);

                                        string MusicPath = Directory.GetFiles(docPath + "\\Extracted Sounds", currentItem + ".ogg", SearchOption.AllDirectories).FirstOrDefault();
                                        if (!File.Exists(MusicPath))
                                        {
                                            var filesPath = Directory.GetFiles(docPath + "\\Extracted", currentItem + ".uexp", SearchOption.AllDirectories).FirstOrDefault();
                                            if (!File.Exists(filesPath))
                                            {
                                                await Task.Run(() => {
                                                    jwpmProcess("extract \"" + Config.conf.pathToFortnitePAKs + "\\" + currentPAK + "\" \"" + currentItem + "\" \"" + docPath + "\"");
                                                });
                                                filesPath = Directory.GetFiles(docPath + "\\Extracted", currentItem + ".uexp", SearchOption.AllDirectories).FirstOrDefault();
                                            }
                                            try
                                            {
                                                if (filesPath != null)
                                                {
                                                    AppendText("✔ ", Color.Green);
                                                    AppendText(currentItem, Color.DarkRed);
                                                    AppendText(" successfully extracted to ", Color.Black);
                                                    AppendText(filesPath.Substring(0, filesPath.LastIndexOf('.')), Color.SteelBlue, true);
                                                    try
                                                    {
                                                        convertToOGG(filesPath, currentItem);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Console.WriteLine(ex.Message);
                                                    }

                                                    MusicPath = docPath + "\\Extracted Sounds\\" + Path.GetFileNameWithoutExtension(filesPath) + ".ogg";
                                                    AppendText("✔ ", Color.Green);
                                                    AppendText(currentItem, Color.DarkRed);
                                                    AppendText(" successfully converted to an OGG sound with path ", Color.Black);
                                                    AppendText(MusicPath, Color.SteelBlue, true);
                                                }
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                                AppendText("[IndexOutOfRangeException] ", Color.Red);
                                                AppendText("Can't extract ", Color.Black);
                                                AppendText(currentItem, Color.SteelBlue);
                                                AppendText(" in ", Color.Black);
                                                AppendText(PAKsComboBox.SelectedItem.ToString(), Color.DarkRed, true);
                                            }
                                        }
                                        OpenWithDefaultProgramAndNoFocus(MusicPath);
                                    } //ASSET IS A SOUND => CONVERT AND LOAD SOUND
                                }
                            }
                        }
                        else
                        {
                            AppendText("✗ ", Color.Red);
                            AppendText("No serialized file found", Color.Black, true);
                        }
                    }
                    if (files.Contains(".ufont")) //ASSET IS A FONT => CONVERT TO FONT
                    {
                        convertToOTF(files, currentItem);
                    }
                    if (files.Contains(".ini"))
                    {
                        ItemRichTextBox.Text = File.ReadAllText(files);
                    }
                }
                else
                {
                    AppendText("", Color.Black, true);
                    AppendText("✗ ", Color.Red);
                    AppendText(" Error while extracting ", Color.Black);
                    AppendText(currentItem, Color.SteelBlue, true);
                }
            }
            ExtractButton.Enabled = true;

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            AppendText("\nDone\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\tTime elapsed: " + elapsedTime, Color.Green, true);
        }

        private void LoadImageCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (LoadDataCheckBox.Checked == true)
            {
                SaveImageButton.Enabled = true;
                SaveImageCheckBox.Enabled = true;
            }
            if (LoadDataCheckBox.Checked == false)
            {
                SaveImageButton.Enabled = false;
                SaveImageCheckBox.Enabled = false;
            }
        }
        private void SaveImageCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (SaveImageCheckBox.Checked == true)
            {
                SaveImageButton.Enabled = false;
            }
            if (SaveImageCheckBox.Checked == false)
            {
                SaveImageButton.Enabled = true;
            }
        }

        private void SaveImageButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveTheDialog = new SaveFileDialog();
            saveTheDialog.Title = "Save Icon";
            saveTheDialog.Filter = "PNG Files (*.png)|*.png";
            saveTheDialog.InitialDirectory = docPath + "\\Generated Icons\\";
            saveTheDialog.FileName = ItemName;
            if (saveTheDialog.ShowDialog() == DialogResult.OK)
            {
                ItemIconPictureBox.Image.Save(saveTheDialog.FileName, ImageFormat.Png);
                AppendText("✔ ", Color.Green);
                AppendText(ItemName, Color.DarkRed);
                AppendText(" successfully saved to ", Color.Black);
                AppendText(saveTheDialog.FileName, Color.SteelBlue, true);
            }
        }
    }
}
