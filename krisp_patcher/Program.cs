using System;
using System.IO;
using System.Runtime.InteropServices;
using dnlib.DotNet.Emit;
using dnpatch;

namespace krisp_patcher
{
    class Program
    {
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsUserAnAdmin();

        static bool prompt(string question)
        {
            Console.Write($"{question} ([y/Y] yes, [anything] no) > ");
            ConsoleKeyInfo c = Console.ReadKey();
            Console.Write("\n");
            return c.Key == ConsoleKey.Y;
        }


        static class patch_plan
        {
            public static Instruction[] opCodes = {
                Instruction.Create(OpCodes.Ldstr, "unlimited"),
                Instruction.Create(OpCodes.Ret)
            };

            public static Target target = new Target()
            {
                Namespace = "Krisp.BackEnd",
                Class = "Mode",
                Method = "get_name",
                Instructions = opCodes
            };

            public static bool patch(Patcher patcher)
            {
                bool patched = false;
                Instruction[] insts = patcher.GetInstructions(patch_plan.target);
                if (insts.Length < 3) {
                    Console.WriteLine("Account plan already patched");
                } else {
                    Console.WriteLine("Description: sets your plan to unlimited localy");
                    if (!prompt("Apply unlimited plan patch ? (recommended patch)")) {
                        Console.Write("\n");
                        return patched;
                    }
                    Console.WriteLine($"\nMode.get_name (sz: {insts.Length}) - old");
                    for (int i = 0; i < insts.Length; i++) Console.WriteLine($"{i.ToString("X").PadLeft(2, '0')}    ->  {insts[i].OpCode.Code}");
                    patcher.Patch(patch_plan.target);
                    patched = true;
                    Console.WriteLine($"\nMode.get_name (sz: {patch_plan.opCodes.Length}) - new");
                    for (int i = 0; i < patch_plan.opCodes.Length; i++) Console.WriteLine($"{i.ToString("X").PadLeft(2, '0')}    ->  {patch_plan.opCodes[i].OpCode.Code}");
                    Console.WriteLine("\n+> Patched account plan\n");
                }
                return patched;
            }
        }

        static class patch_minutes
        {
            public static Target target = new Target() {
                Namespace = "Krisp.BackEnd",
                Class = "MinutesBalanceRequestInfo",
                Method = ".ctor",
                Indices = new[] { 0x3,  0x4,  0x5,  0x6,  0x7, /* ((minutesUsage == null) ? Method.GET : Method.POST) => Method.GET */ 
                                  0x14, 0x15, 0x16             /*base.body = minutesUsage; => -*/
                }
            };

            public static bool patch(Patcher patcher)
            {
                bool patched = false;
                Instruction[] insts = patcher.GetInstructions(patch_minutes.target);
                if (insts.Length < 24) {
                    Console.WriteLine("Minutes usage reporting already patched");
                } else {
                    Console.WriteLine("Description: stops Krisp from reporting usage time, making you not loose free minutes");
                    if (!prompt("Apply minutes usage patch ? (probably useless if unlimited plan patch is applied)")) {
                        Console.Write("\n");
                        return patched;
                    }
                    Console.WriteLine($"\nMinutesBalanceRequestInfo..ctor (sz: {insts.Length}) - old");
                    for (int i = 0; i < insts.Length; i++) Console.WriteLine($"{i.ToString("X").PadLeft(2, '0')}    ->  {insts[i].OpCode.Code}");
                    patcher.RemoveInstruction(patch_minutes.target);
                    patched = true;
                    Console.WriteLine($"\nMinutesBalanceRequestInfo..ctor (sz: {insts.Length - patch_minutes.target.Indices.Length}) - new");

                    int skiped = 0;
                    for (int i = 0; i < insts.Length; i++) {
                        if (Array.IndexOf(patch_minutes.target.Indices, i) != -1) {
                            skiped++;
                            continue;
                        }
                        Console.WriteLine($"{(i - skiped).ToString("X").PadLeft(2, '0')}    ->  {insts[i].OpCode.Code}");
                    };
                    Console.WriteLine("\n+> Patched minutes usage reporting\n");
                }
                return patched;
            }
        }

        static void Main(string[] args)
        {
            bool backup_existed = false;
            bool backup_deleted = false;
            Patcher patcher;
            bool patched;
            if (!IsUserAnAdmin()) {
                Console.WriteLine("This program must be run as an Administrator");
                Console.ReadKey();
                return;
            }

            if (!Directory.Exists("C:\\Program Files\\Krisp")) {
                Console.WriteLine("Krisp not installation ('C:\\Program Files\\Krisp') not found");
                Console.ReadKey();
                return;
            }

            Directory.SetCurrentDirectory("C:\\Program Files\\Krisp");

            if (File.Exists("Krisp.exe.bak")) {
                backup_existed = true;
                if (prompt("Backup found, do you want to restore?")) {
                    File.Delete("Krisp.exe");
                    File.Move("Krisp.exe.bak", "Krisp.exe");
                    Console.WriteLine("Backup restored\n");
                    backup_deleted = true;
                }
            }

            if (!File.Exists("Krisp.exe")) {
                Console.WriteLine($"Krisp executable not found in '{Directory.GetCurrentDirectory()}'");
                Console.ReadKey();
                return;
            }

            patcher = new Patcher("Krisp.exe", true);

            patched = patch_plan.patch(patcher);
            patched = patch_minutes.patch(patcher) || patched;

            if (patched) {
                Console.WriteLine("Saving patched binary");
                if (backup_deleted || backup_existed)
                    File.Copy("Krisp.exe", "Krisp.exe.bak");
                try {
                    patcher.Save(true);
                } catch(Exception e) {
                    Console.WriteLine($"Failed to write binary\n{e.Message}");
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("Saved succesfully");
            } else {
                Console.WriteLine("Nothing to patch");
                if (!backup_existed)
                    File.Delete("Krisp.exe.bak");
            }
            Console.ReadKey();
        }
    }
}
