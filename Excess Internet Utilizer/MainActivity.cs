using Android.App;
using Android.Widget;
using Android.OS;
using System.Net;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace EIU
{
    [Activity(Label = "Excess Internet Utilizer", MainLauncher = true)]
    public class MainActivity : Activity
    {
        TextView status_bar, speed_Mbit, speed_Mbyte, total_Mb;
        Java.IO.FileWriter st_w;
        System.Timers.Timer renov = new System.Timers.Timer(1000);
        uint bytes_total = 0, prew_bytes = 0, tmp_c = 0; bool is_auto = true;
        SynchronizationContext ccontext;
        Thread uth;
        PowerManager.WakeLock wlock;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);            
            SetContentView(Resource.Layout.Main);
            wlock = ((PowerManager)GetSystemService(PowerService)).NewWakeLock(WakeLockFlags.ScreenDim, "Excess_Internet_Utilizer");
            status_bar = FindViewById<TextView>(Resource.Id.statusText);
            speed_Mbit = FindViewById<TextView>(Resource.Id.speedMbit);
            speed_Mbyte = FindViewById<TextView>(Resource.Id.speedMbyte);
            total_Mb = FindViewById<TextView>(Resource.Id.totalUtil);
            FindViewById<Button>(Resource.Id.stopButton).Click += MainActivity_Click;
            ccontext = SynchronizationContext.Current;
            uth = new Thread(Utilizethreat);
            renov.Elapsed += Renov_Elapsed;

            if (Directory.Exists("storage/sdcard0/ExcIntUtil"))
            {
                Java.IO.File config_f = new Java.IO.File("storage/sdcard0/ExcIntUtil/EIU_Log.txt");
                if (!config_f.Exists())
                {
                    config_f.CreateNewFile();     
                }
                st_w = new Java.IO.FileWriter("storage/sdcard0/ExcIntUtil/EIU_Log.txt", true);
                if (CheckConnection())
                {
                    status_bar.SetText("Проверка файла конфигурации...", TextView.BufferType.Normal);
                    config_f = new Java.IO.File("storage/sdcard0/ExcIntUtil/EIU_conf.txt");
                    if (config_f.Exists())
                    {
                        wlock.Acquire();
                        uth.Start();
                    }
                    else
                    {
                        config_f.CreateNewFile();
                        NoConfig();
                    }
                }
                else
                {
                    st_w.Write((new Java.Text.SimpleDateFormat("d.M.yy H:m")).Format(new Java.Util.Date()) + " NIC\r\n");
                    st_w.Close();
                    status_bar.SetTextColor(Android.Graphics.Color.DarkRed);
                    status_bar.SetText("Нет доступа к интернету!", TextView.BufferType.Normal);
                }
            }
            else
            {
                Directory.CreateDirectory("storage/sdcard0/ExcIntUtil");
                Java.IO.File config_f = new Java.IO.File("storage/sdcard0/ExcIntUtil/EIU_Log.txt");
                config_f.CreateNewFile();
                st_w = new Java.IO.FileWriter("storage/sdcard0/ExcIntUtil/EIU_Log.txt", true);
                if (CheckConnection())
                {
                    status_bar.SetText("Проверка файла конфигурации...", TextView.BufferType.Normal);
                    config_f = new Java.IO.File("storage/sdcard0/ExcIntUtil/EIU_conf.txt");
                    config_f.CreateNewFile();
                    NoConfig();
                }
                else
                {
                    st_w.Write((new Java.Text.SimpleDateFormat("d.M.yy H:m")).Format(new Java.Util.Date()) + " NIC\r\n");
                    st_w.Close();
                    status_bar.SetTextColor(Android.Graphics.Color.DarkRed);
                    status_bar.SetText("Нет доступа к интернету!", TextView.BufferType.Normal);
                }
            }
        }

        private void MainActivity_Click(object sender, System.EventArgs e)
        {
            is_auto = false;
            continue_ = false;
            Timer_enable = false;
            status_bar.SetText("Остановлено пользователем.", TextView.BufferType.Normal);
            speed_Mbit.SetText(Getval(0, false) + "/с", TextView.BufferType.Normal);
            speed_Mbyte.SetText(Getval(0, true) + "/с", TextView.BufferType.Normal);

        }

        bool continue_ = true;
        void Utilizethreat()
        {
            string[] rtext = File.ReadAllLines("storage/sdcard0/ExcIntUtil/EIU_conf.txt");
            if (rtext.Length > 0)
            {
                st_w.Write((new Java.Text.SimpleDateFormat("d.M.yy H:m")).Format(new Java.Util.Date()) + " RFC:");
                ccontext.Post(s =>
                {
                    status_bar.SetText("Проверка удалённых файлов...", TextView.BufferType.Normal);
                    FindViewById<LinearLayout>(Resource.Id.utilizite).Visibility = Android.Views.ViewStates.Visible;
                }, null);
                string[] dstr = new string[2];
                bool cf = false;
                for (ushort i = 0; i < rtext.Length; i++)
                {
                    if (Regex.IsMatch(rtext[i], "^(.+)([|]{1})(.+)$"))
                    {
                        dstr[0] = Regex.Match(rtext[i], "^(.+)([|]{1})").ToString().TrimEnd('|');
                        ccontext.Post(s =>
                        {
                            status_bar.SetTextColor(Android.Graphics.Color.CornflowerBlue);
                            status_bar.SetText("Проверка файла \"" + dstr[0] + "\".", TextView.BufferType.Normal);
                        }, null);
                        Timer_enable = true;
                        dstr[1] = Regex.Match(rtext[i], "([|]{1})(.+)$").ToString().Substring(1);
                        cf = true;
                        bool ok_stat = true, be_ok = false;
                        try
                        {
                            do
                            {
                                WebRequest request = WebRequest.Create(dstr[1]);
                                request.Credentials = CredentialCache.DefaultCredentials;
                                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    ccontext.Post(s =>
                                    {
                                        status_bar.SetTextColor(Android.Graphics.Color.Green);
                                        status_bar.SetText("Загрузка файла \"" + dstr[0] + "\".", TextView.BufferType.Normal);
                                    }, null);
                                    Stream dataStream = response.GetResponseStream();
                                    byte[] buffer = new byte[2048]; ushort bytes_read;
                                    bytes_read = (ushort)dataStream.Read(buffer, 0, 2048);
                                    bytes_total += bytes_read;
                                    while (bytes_read > 0 && continue_)
                                    {
                                        bytes_read = (ushort)dataStream.Read(buffer, 0, 2048);
                                        bytes_total += bytes_read;
                                    }
                                    dataStream.Close();
                                    be_ok = true;
                                }
                                else
                                    ok_stat = false;
                                response.Close();
                            } while (ok_stat && continue_);
                        } catch (System.Exception) { }
                        if (be_ok)
                            st_w.Write(" \"" + dstr[0] + "\":S");
                        else
                            st_w.Write(" \"" + dstr[0] + "\":F");
                        Timer_enable = false;
                        if (!CheckConnection() || !continue_)
                        {
                            break;
                        }
                    }
                }
                if (cf)
                {
                    st_w.Write(" T:" + Getval(bytes_total, true) + "\r\n");
                    st_w.Close();
                }
                else
                    ccontext.Post(s => { NoConfig(); }, null);
            }
            else
                ccontext.Post(s => { NoConfig(); }, null);
            wlock.Release();
            if (is_auto)
                ccontext.Post(s => { Dispose_all(); }, null);
        }

        bool timer_en = false, allow_set = true;
        bool Timer_enable { get { return timer_en; } set { timer_en = value; if (allow_set) renov.Enabled = timer_en; } }
        private void Renov_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            allow_set = false;
            renov.Enabled = false;
            tmp_c = bytes_total - prew_bytes;
            prew_bytes = bytes_total;
            ccontext.Post(s =>
            {
                speed_Mbit.SetText(Getval(tmp_c, false) + "/с", TextView.BufferType.Normal);
                speed_Mbyte.SetText(Getval(tmp_c, true) + "/с", TextView.BufferType.Normal);
                total_Mb.SetText(Getval(bytes_total, true), TextView.BufferType.Normal);
            }, null);
            if (timer_en) renov.Enabled = true;
            allow_set = true;
        }

        string Getval(uint bytes, bool isbytes)
        {
            if (isbytes)
            {
                if (bytes >= 1073741824)
                {
                    return System.Math.Round(bytes / 1073741824.0, 2) + " ГБ";
                }
                else if(bytes >= 1048576)
                {
                    return System.Math.Round(bytes / 1048576.0, 2) + " МБ";
                }
                else if(bytes >= 1024)
                {
                    return System.Math.Round(bytes / 1024.0, 2) + " КБ";
                }
                else
                {
                    return bytes + " Б";
                }
            }
            else
            {
                if (bytes >= 134217728)
                {
                    return System.Math.Round(bytes / 134217728.0, 2) + " ГБит";
                }
                else if (bytes >= 131072)
                {
                    return System.Math.Round(bytes / 131072.0, 2) + " МБит";
                }
                else if (bytes >= 128)
                {
                    return System.Math.Round(bytes / 128.0, 2) + " КБит";
                }
                else
                {
                    return (bytes * 8) + " Бит";
                }
            }
        }

        void Dispose_all()
        {
            status_bar.Dispose();
            speed_Mbit.Dispose();
            speed_Mbyte.Dispose();
            total_Mb.Dispose();
            st_w.Dispose();
            renov.Dispose();
            wlock.Dispose();
            status_bar = null;
            speed_Mbit = null;
            speed_Mbyte = null;
            total_Mb = null;
            st_w = null;
            renov = null;
            ccontext = null;
            uth = null;
            wlock = null;
            Finish();
        }


        private void NoConfig()
        {
            st_w.Write((new Java.Text.SimpleDateFormat("d.M.yy H:m")).Format(new Java.Util.Date()) + " CNF\r\n");
            st_w.Close();
            status_bar.SetTextColor(Android.Graphics.Color.DarkRed);
            status_bar.SetText("Файлы для скачивания не найдены!", TextView.BufferType.Normal);
            AlertDialog alert = (new AlertDialog.Builder(this)).Create();
            alert.SetMessage("Введите список прямых ссылок на файлы (удалённые) в файл \"storage/sdcard0/ExcIntUtil/EIU_conf.txt\". Каждый файл должен быть на отдельной строке. Формат: 'имя(для отображения)|файл'.");
            alert.SetButton("OK", (o, ev) => { });
            alert.Show();
        }

        bool CheckConnection()
        {
            try
            {
                bool state = false;
                System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
                ping.Send("google.com", 10000);
                if (ping.Send("google.com", 10000).Status == System.Net.NetworkInformation.IPStatus.Success)

                    state = true;
                return state;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}

