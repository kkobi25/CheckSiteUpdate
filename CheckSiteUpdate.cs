using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace CheckSiteUpdate
{
    class CheckSiteUpdate
    {
        private const string WAVFILENAME = "Bell.wav";
        private const string DEFAULT_URL = @"https://www3.boj.or.jp/market/jp/menu_etf.htm";
        private static ConsoleModeControl cmc = null;

        static readonly private string dateTimeFormat = "yyyy/MM/dd HH:mm:ss";

        static private string str_URL = "";
        static private bool isDebugMode = false;
        static private int SleepMS = 1000;
        static private bool hasPrevFailed = false;
        static private List<Task> SiteCheckTasks = new List<Task>();
        static private object lckObj = new object();
        static void Main(string[] args)
        {
            
            if (!ReadArgs(args))
            {
                Console.WriteLine("Invalid arguments.");
                Environment.Exit(0xA0);
            }

            cmc = new ConsoleModeControl();
            cmc.DisableQuickEdit();

            SiteCheckTasks.Add(CheckSiteUpdateAsync(str_URL));

            var taskCheckTast = TaskCheckAsync(SiteCheckTasks);

            using (var manualResetEvent = new ManualResetEvent(false))
            {
                manualResetEvent.WaitOne();
                Console.WriteLine("Exiting...");
            }
            return;
        }

        static private async Task CheckSiteUpdateAsync(string URL)
        {
            DateTime lastUpdateTime = DateTime.MinValue;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    lastUpdateTime = GetLastUpdateTime(URL);
                }
                catch
                {
                    lastUpdateTime = DateTime.MinValue;
                    Thread.Sleep(5000);
                    continue;
                }
                break;
            }

            if (lastUpdateTime == DateTime.MinValue)
            {
                Console.WriteLine("問題が発生しました。");
                cmc?.RestoreOriginalMode();
                Environment.Exit(0);
            }
            Console.WriteLine("スタート!");
            Console.WriteLine("URL: {0}", URL);
            Console.WriteLine("このサイトは\"{0}\"に更新されました。\nサイトが更新されると音がなります。（周りの環境に注意してください）", lastUpdateTime.ToString(dateTimeFormat));

            hasPrevFailed = false;

            await Task.Run(() =>
            {                
                while (true)
                {
                    Thread.Sleep(SleepMS);

                    DateTime newUpdateTime = DateTime.MinValue;
                    try
                    {
                        newUpdateTime = GetLastUpdateTime(URL);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"エラー：{e.Message}");
                        newUpdateTime = DateTime.MinValue;
                    }

                    if (newUpdateTime == DateTime.MinValue)
                    {
                        hasPrevFailed = true;
                        Console.WriteLine("リトライ中…しばらくしても復旧しない場合は、アプリを再起動してください。");
                        continue;
                    }
                    if (isDebugMode)
                    {
                        Console.WriteLine("Debug Mode: ({0}) Update time = {1}, URL = {2}", DateTime.Now.ToString(dateTimeFormat), newUpdateTime.ToString(dateTimeFormat), URL);
                    }

                    if (hasPrevFailed)
                    {
                        //Success this time
                        hasPrevFailed = false;
                        Console.WriteLine($"アプリは正常に動作しています。最終更新時刻：\"{newUpdateTime.ToString(dateTimeFormat)}\"");
                    }
                    if (lastUpdateTime < newUpdateTime)
                    {
                        lock (lckObj)
                        {
                            Console.WriteLine("サイトが更新されました。更新時刻：{0}", newUpdateTime.ToString(dateTimeFormat));

                            try
                            {
                                // 現在実行しているアセンブリを取得する
                                var assm = Assembly.GetExecutingAssembly();
                                using (var stream = assm.GetManifestResourceStream($"{assm.GetName().Name}.{WAVFILENAME}"))
                                {
                                    using (var soundPlayer = new System.Media.SoundPlayer(stream))
                                    {
                                        soundPlayer.Play();
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"音源の再生に失敗しました：{e.Message}");
                            }

                            System.Windows.Forms.MessageBox.Show($"サイト({URL})が更新されました。更新時刻：{newUpdateTime.ToString(dateTimeFormat)}",
                                "更新されました",
                                System.Windows.Forms.MessageBoxButtons.OK);

                            // Restore console mode and prepare to finish
                            cmc?.RestoreOriginalMode();
                            while (true)
                            {
                                Console.Write("アプリを終了しますか？（Y/N）：");
                                string res = Console.ReadLine();
                                if (res.ToUpper().Trim() == "Y")
                                {
                                    Console.WriteLine("アプリを終了します。");
                                    Environment.Exit(0);
                                }
                                else if (res.ToUpper().Trim() == "N")
                                {
                                    Console.WriteLine("監視を継続します。");
                                    cmc?.DisableQuickEdit();
                                    break;
                                }
                            }
                        }
                    }
                    lastUpdateTime = newUpdateTime;
                }
            });
        }

        static private async Task TaskCheckAsync(IEnumerable<Task> tasks)
        {
            int counter = 0;
            await Task.Run(() =>
            {                
                while (true)
                {
                    if (tasks == null)
                    {
                        Console.WriteLine("異常が発生しました。プログラムを再起動してください。");
                        break;
                    }

                    Thread.Sleep(1000);
                    bool isAllRunning = true;
                    foreach (var task in tasks)
                    {
                        if (task == null)
                        {
                            isAllRunning = false;
                            break;
                        }
                        if (task.IsCompleted || task.IsFaulted || task.IsCanceled)
                        {
                            isAllRunning = false;
                            break;
                        }
                    }

                    if (!isAllRunning)
                    {
                        Console.WriteLine("プログラムが停止しています。再起動してください。");
                        break;
                    }

                    lock (lckObj)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        switch (counter % 4)
                        {
                            case 1:
                                Console.Write("監視中-");
                                break;
                            case 2:
                                Console.Write("監視中\\");
                                break;
                            case 3:
                                Console.Write("監視中|");
                                break;
                            default:
                                Console.Write("監視中/");
                                break;
                        }
                    }
                    counter = (counter < int.MaxValue) ? counter + 1 : 0;
                }
            });
        }

        static private bool ReadArgs(string[] args)
        {
            if (args == null)
                return false;

            if (args.Length <= 0)
            {
                str_URL = DEFAULT_URL;
                isDebugMode = false;
            }
            else if (args.Length == 1)
            {
                str_URL = args[0].Trim(' ', '"', '\'');
                isDebugMode = false;
            }
            else
            {
                isDebugMode = false;
                str_URL = "";
                SleepMS = 1000;
                bool isSleepSpecifier = false;
                foreach (string arg in args)
                {
                    if (arg.StartsWith(@"\"))
                    {
                        if (!isDebugMode && arg.ToLower().Trim(' ', '"', '\'') == @"\d")
                        {
                            isDebugMode = true;
                        }
                        else if (arg.ToLower().Trim(' ', '"', '\'') == @"\s")
                        {
                            isSleepSpecifier = true;
                            continue; // use continue command here to avoid isSleepSpecifier to become false.
                        }

                    }
                    else if (isSleepSpecifier)
                    {
                        int sleepMS = 1000;
                        if (!int.TryParse(arg, out sleepMS))
                            sleepMS = 1000;
                        SleepMS = Math.Max(500, Math.Min(sleepMS, 60000));
                    }
                    else if (string.IsNullOrEmpty(str_URL))
                    {
                        str_URL = arg;
                    }
                    isSleepSpecifier = false;
                }
            }

            return true;
        }

        static private DateTime GetLastUpdateTime(string url)
        {
            DateTime lastUpdateTime = DateTime.MinValue;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 10000;

                // Get response async to set timeout
                var taskResponse = req.GetResponseAsync();
                if (!taskResponse.Wait(10000))
                {
                    throw new TimeoutException("Failed to get response within timeout period");
                }
                if (taskResponse.Exception != null)
                {
                    throw taskResponse.Exception;
                }
                else if (taskResponse.Result == null)
                {
                    throw new NullReferenceException("Response is null");
                }
                else if (!(taskResponse.Result is HttpWebResponse))
                {
                    throw new InvalidCastException("Failed to parse to HttpWebResponse.");
                }
                
                using (HttpWebResponse res = (HttpWebResponse)taskResponse.Result)
                {
                    lastUpdateTime = res.LastModified;
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            return lastUpdateTime;
        }
    }
}
