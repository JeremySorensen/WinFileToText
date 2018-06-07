using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace WinFileToText {

    struct Details {
        public string FileName;
        public string Attributes;
        public string CreationTime;
        public string WriteTime;
        public string AccessTime;
    }

    class Program {

        static Details GetDetails(string path) {
            var details = new Details();
            details.FileName = null;
            details.Attributes = File.GetAttributes(path).ToString();
            details.CreationTime = File.GetCreationTimeUtc(path).ToString();
            details.WriteTime = File.GetLastWriteTimeUtc(path).ToString();
            details.AccessTime = File.GetLastAccessTimeUtc(path).ToString();
            return details;
        }

        static void SetDetails(string path, Details details) {
            var a = (FileAttributes)Enum.Parse(typeof(FileAttributes), details.Attributes);
            var c = DateTime.Parse(details.CreationTime);
            var w = DateTime.Parse(details.WriteTime);
            var t = DateTime.Parse(details.AccessTime);

            File.SetAttributes(path, a);
            File.SetCreationTimeUtc(path, c);
            File.SetLastWriteTimeUtc(path, w);
            File.SetLastAccessTimeUtc(path, t);
        }

        static byte NybbleToHex(byte nybble) {
            if (nybble < 10) {
                return (byte)(0x30 + nybble);
            } else {
                return (byte)(0x41 + nybble - 10);
            }
        }

        static byte[] ByteToHex(byte data) {
            var result = new byte[2];
            result[0] = NybbleToHex((byte)((data >> 4) & 0xF));
            result[1] = NybbleToHex((byte)(data & 0xF));
            return result;
        }

        static void BytesToHex(byte[] data, byte[] hex) {
            for (int i = 0; i < data.Length; ++i) {
                var b = ByteToHex(data[i]);
                hex[i * 2] = b[0];
                hex[i * 2 + 1] = b[1];
            }
        }

        static byte HexToNybble(byte nybble) {
            if (nybble < 0x41) {
                return (byte)(nybble - 0x30);
            } else {
                return (byte)(nybble - 0x41 + 10);
            }
        }

        static byte HexToByte(byte[] data) {
            return (byte)((HexToNybble(data[0]) << 4) | (HexToNybble(data[1])));
        }

        static void HexToBytes(byte[] hex, byte[] data) {
            for (int i = 0; i < hex.Length / 2; ++i) {
                data[i] = HexToByte(new byte[] { hex[i * 2], hex[i * 2 + 1] });
            }
        }

        static void WriteHeaderText(string inPath, string outPath) {
            using (var output = new StreamWriter(outPath)) {
                output.NewLine = "\n";
                output.WriteLine(Path.GetFileName(inPath));
                var details = GetDetails(inPath);
                output.WriteLine(details.Attributes);
                output.WriteLine(details.CreationTime);
                output.WriteLine(details.WriteTime);
                output.WriteLine(details.AccessTime);
                output.WriteLine();
            }
        }

        static void WriteBodyHex(string inPath, string outPath) {
            const int NUM_BYTES = 2 * 1024;
            var hexData = new byte[NUM_BYTES * 2];
            using (var output = new BinaryWriter(new FileStream(outPath, FileMode.Append)))
            using (var input = new BinaryReader(new FileStream(inPath, FileMode.Open))) {
                while (true) {
                    var data = input.ReadBytes(NUM_BYTES);
                    BytesToHex(data, hexData);
                    output.Write(hexData, 0, data.Length * 2);
                    if (data.Length < NUM_BYTES) {
                        return;
                    }
                }
            }
        }

        static void WriteAsStringFile(string inPath, string outPath) {
            WriteHeaderText(inPath, outPath);
            WriteBodyHex(inPath, outPath);
        }

        static Details ReadDetails(string inPath) {
            var details = new Details();
            using (var input = new StreamReader(inPath)) {
                details.FileName = input.ReadLine();
                details.Attributes = input.ReadLine();
                details.CreationTime = input.ReadLine();
                details.WriteTime = input.ReadLine();
                details.AccessTime = input.ReadLine();
            }
            return details;
        }

        static void WriteFile(string outPath, string inPath) {
            const int NUM_BYTES = 2 * 1024;
            var data = new byte[NUM_BYTES];
            var start = GetDataStart(inPath);
            using (var stream = new FileStream(inPath, FileMode.Open)) {
                stream.Seek(start, SeekOrigin.Begin);
                using (var input = new BinaryReader(stream))
                using (var output = new BinaryWriter(new FileStream(outPath, FileMode.Create))) {
                    while (true) {
                        var hexData = input.ReadBytes(2 * NUM_BYTES);
                        HexToBytes(hexData, data);
                        output.Write(data, 0, hexData.Length / 2);
                        if (hexData.Length < 2 * NUM_BYTES) {
                            return;
                        }
                    }
                }
            }
        }

        static int GetDataStart(string path) {
            using (var input = new BinaryReader(new FileStream(path, FileMode.Open))) {
                var data = input.ReadBytes(1024);
                bool foundNewline = false;
                for (int i = 0; i < data.Length; ++i) {
                    if (data[i] == '\n') {
                        if (foundNewline) {
                            return i + 1;
                        } else {
                            foundNewline = true;
                        }
                    } else {
                        foundNewline = false;
                    }
                }
            }
            return -1;
        }

        static void WriteAsBinFile(string inPath) {
            var details = ReadDetails(inPath);
            WriteFile(details.FileName, inPath);
            SetDetails(details.FileName, details);
        }

        static void Usage() {
            Console.WriteLine("usage: WinFileToText text [SOURCE] [DEST]");
            Console.WriteLine("   or: WinFileToText bin [SOURCE]");
        }

        static void Main(string[] args) {
            bool toText = false;
            if (args.Length == 2) {
                if (args[0].ToLower() != "bin") {
                    Usage();
                    return;
                }
            } else if (args.Length == 3) {
                if (args[0].ToLower() != "text") {
                    Usage();
                    return;
                }
                toText = true;
            } else {
                Usage();
                return;
            }

            try {
                if (toText) {
                    WriteAsStringFile(args[1], args[2]);
                } else {
                    WriteAsBinFile(args[1]);
                }
            } catch (Exception e) {
                Console.WriteLine($"Error: {e.Message}");
            }

        }
    }
}
