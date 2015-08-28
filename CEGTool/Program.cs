using System;
using System.IO;
using System.Text;
using System.Drawing;

namespace CEGTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("The Punisher *.ceg Texture Dumper/Creator by gdkchan");
            Console.WriteLine("Version 0.1.1");
            Console.CursorTop++;
            Console.ResetColor();

            if (args.Length == 0)
            {
                PrintUsage();
            }
            else
            {
                string Operation = args[0];
                string FileName = null;

                if (args.Length == 2)
                {
                    FileName = args[1];
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: Invalid number of arguments!");
                    Console.CursorTop++;
                    PrintUsage();
                    return;
                }

                switch (Operation)
                {
                    case "-d":
                        if (FileName == "-all")
                        {
                            string[] Files = Directory.GetFiles(Environment.CurrentDirectory);
                            foreach (string File in Files) if (Path.GetExtension(File).ToLower() == ".ceg") Dump(File);
                        }
                        else
                            Dump(FileName);

                        break;
                    case "-c":
                        if (FileName == "-all")
                        {
                            string[] Folders = Directory.GetDirectories(Environment.CurrentDirectory);
                            foreach (string Folder in Folders) Create(Folder);
                        }
                        else
                            Create(FileName);

                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: Invalid operation specified!");
                        Console.CursorTop++;
                        PrintUsage();
                        break;
                }
            }
        }

        static void PrintUsage()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Usage:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("CEGTool.exe [operation] [file|-all]");
            Console.CursorTop++;
            Console.WriteLine("[operation]");
            Console.WriteLine("-d  Dumps a *.ceg file to a folder");
            Console.WriteLine("-c  Creates a *.ceg file from a folder");
            Console.CursorTop++;
            Console.WriteLine("-all  Manipulate all the files on the work directory");
            Console.CursorTop++;
            Console.WriteLine("Example:");
            Console.WriteLine("CEGTool -d file.ceg");
            Console.WriteLine("CEGTool -d -all");
            Console.WriteLine("CEGTool -c folder");
            Console.WriteLine("CEGTool -c -all");
            Console.ResetColor();
        }

        private static void Dump(string FileName)
        {
            FileStream Input = new FileStream(FileName, FileMode.Open);
            BinaryReader Reader = new BinaryReader(Input);

            string OutDir = Path.GetFileNameWithoutExtension(FileName);
            Directory.CreateDirectory(OutDir);

            uint Signature = Reader.ReadUInt32(); //GEKV
            uint Version = Reader.ReadUInt32();
            uint HeaderLength = Reader.ReadUInt32();
            uint FileLength = Reader.ReadUInt32();
            uint FileCount = Reader.ReadUInt32();

            for (int i = 0; i < FileCount; i++)
            {
                Input.Seek(0x20 + i * 0x30, SeekOrigin.Begin);

                uint Offset = Reader.ReadUInt32();
                int Width = Reader.ReadUInt16();
                int Height = Reader.ReadUInt16();
                uint Descriptor = Reader.ReadUInt32();
                Reader.ReadUInt32();

                byte Format = (byte)((Descriptor >> 16) & 0xff);

                string Name = ReadString(Reader, (uint)Input.Position);
                Input.Seek(0x18, SeekOrigin.Current);
                uint Length = Reader.ReadUInt32();

                Input.Seek(Offset, SeekOrigin.Begin);
                byte[] Data = new byte[Length];
                Reader.Read(Data, 0, Data.Length);
                Bitmap FullImage = null;
                switch (Format)
                {
                    case 1:
                        Input.Seek(Offset, SeekOrigin.Begin);
                        Data = new byte[Width * Height * 4];
                        Reader.Read(Data, 0, Data.Length);
                        FullImage = new Bitmap(Width, Height);

                        uint DataOffset = 0;
                        for (int Y = 0; Y < Height; Y++)
                        {
                            for (int X = 0; X < Width; X++)
                            {
                                int B = Data[DataOffset++];
                                int G = Data[DataOffset++];
                                int R = Data[DataOffset++];
                                int A = Data[DataOffset++];

                                FullImage.SetPixel(X, Y, Color.FromArgb(A, R, G, B));
                            }
                        }
                        break;
                    case 2:
                    case 3: //Guesswork, may be wrong
                        FullImage = DXTCodec.DXT3_Decode(Data, POW2RoundUp(Width), POW2RoundUp(Height)); break;
                    case 4: 
                    case 5:
                        FullImage = DXTCodec.DXT5_Decode(Data, POW2RoundUp(Width), POW2RoundUp(Height)); break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warning: Image \"" + Name + "\" on file \"" + Path.GetFileName(FileName) + "\" have an unknow format!");
                        Console.ResetColor();
                        continue;
                }
                
                Bitmap Img = new Bitmap(Width, Height);
                Graphics g = Graphics.FromImage(Img);
                g.DrawImage(FullImage, new Rectangle(0, 0, Width, Height), new Rectangle(0, 0, Width, Height), GraphicsUnit.Pixel);
                g.Dispose();
                Img.Save(Path.Combine(OutDir, Name + ".png"));
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Dumped file \"" + FileName + "\"!");
            Console.ResetColor();
        }

        /// <summary>
        ///     Read an ASCII String from a given Reader at a given address.
        ///     Note that the text MUST end with a Null Terminator (0x0).
        ///     It doesn't advances the position after reading.
        /// </summary>
        /// <param name="Input">The Reader of the File Stream</param>
        /// <param name="Address">Address where the text begins</param>
        /// <returns></returns>
        public static string ReadString(BinaryReader Input, uint Address)
        {
            long OriginalPosition = Input.BaseStream.Position;
            Input.BaseStream.Seek(Address, SeekOrigin.Begin);
            MemoryStream Bytes = new MemoryStream();
            for (;;)
            {
                byte b = Input.ReadByte();
                if (b == 0) break;
                Bytes.WriteByte(b);
            }
            Input.BaseStream.Seek(OriginalPosition, SeekOrigin.Begin);
            return Encoding.ASCII.GetString(Bytes.ToArray());
        }

        private static int POW2RoundUp(int Value)
        {
            return (int)Math.Pow(2, Math.Ceiling(Math.Log(Value) / Math.Log(2)));
        }

        private static void Create(string Folder)
        {
            string[] Files = Directory.GetFiles(Folder, "*.png");

            FileStream Output = new FileStream(Folder + ".ceg", FileMode.Create);
            BinaryWriter Writer = new BinaryWriter(Output);

            Writer.Write((uint)0x564b4547); //GEKV Signature
            Writer.Write((uint)1); //Version
            Writer.Write((uint)(Files.Length * 0x30)); //Header length (texture entries sect only)
            Writer.Write((uint)0); //File length (lets add this one later)
            Writer.Write((uint)Files.Length);
            Writer.Write((uint)0);
            Writer.Write((uint)Files.Length);
            Writer.Write((uint)0x80);

            int DataOffset = 0x20 + Files.Length * 0x30;
            for (int i = 0; i < Files.Length; i++)
            {
                string Name = Path.GetFileNameWithoutExtension(Path.GetFileName(Files[i]));
                if (Name.Length > 23) Name = Name.Substring(0, 23); //23 is the max file length (it needs a Null Terminator)

                Bitmap Img = new Bitmap(Files[i]);
                Bitmap NewImage = new Bitmap(POW2RoundUp(Img.Width), POW2RoundUp(Img.Height));
                Graphics g = Graphics.FromImage(NewImage);
                g.DrawImage(Img, new Rectangle(0, 0, Img.Width, Img.Height), new Rectangle(0, 0, Img.Width, Img.Height), GraphicsUnit.Pixel);
                g.Dispose();
                byte[] Data = DXTCodec.DXT5_Encode(NewImage);

                Output.Seek(0x20 + i * 0x30, SeekOrigin.Begin);
                Writer.Write((uint)DataOffset);
                Writer.Write((ushort)Img.Width);
                Writer.Write((ushort)Img.Height);
                Writer.Write((uint)0x104020f);
                Writer.Write((uint)0x960100);
                Writer.Write(Encoding.ASCII.GetBytes(Name));
                Output.Seek(24 - Name.Length, SeekOrigin.Current);
                Writer.Write((uint)Data.Length);

                Output.Seek(DataOffset, SeekOrigin.Begin);
                Writer.Write(Data);
                DataOffset += Data.Length;
            }

            Output.Seek(0xc, SeekOrigin.Begin);
            Writer.Write((uint)Output.Length);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Created file \"" + Folder + ".ceg\"!");
            Console.ResetColor();
        }
    }
}
