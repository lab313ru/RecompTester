using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using tester.Properties;

namespace tester
{
    class Program
    {
        public static bool FilesContentsAreEqual(FileInfo fileInfo1, FileInfo fileInfo2)
        {
            bool result;

            if (fileInfo1.Length != fileInfo2.Length)
            {
                result = false;
            }
            else
            {
                using (var file1 = fileInfo1.OpenRead())
                {
                    using (var file2 = fileInfo2.OpenRead())
                    {
                        result = StreamsContentsAreEqual(file1, file2);
                    }
                }
            }

            return result;
        }

        private static bool StreamsContentsAreEqual(Stream stream1, Stream stream2)
        {
            const int bufferSize = 2048 * 2;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];

            while (true)
            {
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);

                if (count1 != count2)
                {
                    return false;
                }

                if (count1 == 0)
                {
                    return true;
                }

                int iterations = (int)Math.Ceiling((double)count1 / sizeof(Int64));
                for (int i = 0; i < iterations; i++)
                {
                    if (BitConverter.ToInt64(buffer1, i * sizeof(Int64)) != BitConverter.ToInt64(buffer2, i * sizeof(Int64)))
                    {
                        return false;
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            List<string> lines = new List<string>();

            if (!File.Exists(Settings.Default.LogPath))
            {
                Console.WriteLine(string.Format("Error: Cannot find log-file: {0}", Path.GetFileName(Settings.Default.LogPath)));
                return;
            }

            lines.AddRange(File.ReadAllLines(Settings.Default.LogPath));

            Regex regex = new Regex(Settings.Default.LineRegex);
            for (int i = 0; i < lines.Count; ++i)
            {
                Match match = regex.Match(lines[i]);

                if (match.Success)
                {
                    int addr = Convert.ToInt32(match.Groups[1].Value, 16);
                    long csize = Convert.ToInt32(match.Groups[2].Value, 10);
                    long dsize = Convert.ToInt32(match.Groups[3].Value, 10);

                    string bin_file = string.Format(Settings.Default.BinFileMask, addr);
                    string cmp_file = string.Format(Settings.Default.CmpFileMask, addr);
                    string dec_file = string.Format(Settings.Default.DecFileMask, addr);

                    Process exe = new Process();
                    exe.StartInfo.UseShellExecute = false;
                    exe.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    exe.StartInfo.RedirectStandardOutput = true;

                    if (!File.Exists(Settings.Default.ToolFile))
                    {
                        Console.WriteLine(string.Format("Error: Cannot find tool-file: {0}", Path.GetFileName(Settings.Default.ToolFile)));
                        return;
                    }

                    exe.StartInfo.FileName = Settings.Default.ToolFile;
                    exe.StartInfo.Arguments = string.Format(Settings.Default.ToolFileArgsCmpMask, bin_file, cmp_file);
                    exe.Start();
                    exe.WaitForExit(1000 * 60 * 5);
                    
                    long cmp_size = (new FileInfo(cmp_file).Length);

                    exe.StartInfo.Arguments = string.Format(Settings.Default.ToolFileArgsDecMask, cmp_file, dec_file);
                    exe.Start();
                    exe.WaitForExit(1000 * 60 * 5);

                    Console.Write("[{0:000}, {1:X6}]: ", i, addr);

                    bool bin_cmp_equal = false;
                    if (File.Exists(bin_file))
                    {
                        if (File.Exists(dec_file))
                        {
                            bin_cmp_equal = FilesContentsAreEqual(new FileInfo(bin_file), new FileInfo(dec_file));
                        }
                        else
                        {
                            Console.WriteLine(string.Format("cannot find file! ({0})", Path.GetFileName(dec_file)));
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine(string.Format("cannot find file! ({0})", Path.GetFileName(bin_file)));
                        continue;
                    }

                    if (!bin_cmp_equal)
                    {
                        Console.WriteLine("files are not equal!");
                    }
                    else
                    {
                        Console.Write(string.Format("ORIG: {0:X4} | MY: {1:X4} | DIFF: ", csize, cmp_size));

                        if (csize > cmp_size)
                        {
                            Console.Write("-");
                        }
                        else if (csize == cmp_size)
                        {
                            Console.Write("=");
                        }
                        else
                        {
                            Console.Write("+");
                        }

                        Console.WriteLine("({0})", Math.Abs(csize - cmp_size));

                        File.Delete(cmp_file);
                        File.Delete(dec_file);
                    }
                }
            }
        }
    }
}
