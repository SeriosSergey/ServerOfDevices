using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Web;

namespace СерврерУстройств
{
    class Program
    {
        static List<Пользователь> список_пользователей = new List<Пользователь>();
        static List<Событие> список_событий = new List<Событие>();
        static List<Сеанс> список_сеансов = new List<Сеанс>();
        static List<Устройство> список_устройств = new List<Устройство>();
        static List<СписокОшибокУстройства> список_ошибок = new List<СписокОшибокУстройства>();
        static List<Скрипт> список_скриптов = new List<Скрипт>();
        static Сервер сервер;

        static void Main(string[] args)
        {
            /*
            Пользователь пользователь = new Пользователь();
            пользователь.логин = "Sergey";
            пользователь.пароль = "123";
            пользователь.класс = "Администратор";
            список_пользователей.Add(пользователь);
            
            Событие событие = new Событие();
            событие.время = DateTime.Now;
            событие.текст = "Пользователь вошел в систему.";
            пользователь.список_событий.Add(событие);

            событие = new Событие();
            событие.время = DateTime.Now;
            событие.текст = "Пользователь вышел из системы.";
            пользователь.список_событий.Add(событие);

            список_пользователей.Add(пользователь);
            список_пользователей.Add(пользователь);
            */
            //Запись_пользователей_на_диск();
            Чтение_устройств_с_диска();
            //Запись_устройств_на_диск();
            Чтение_событий_с_диска();
            Чтение_пользователей_с_диска();

            Thread поток_работы_со_списками = new Thread(Работа_со_списками);
            поток_работы_со_списками.IsBackground = true;
            поток_работы_со_списками.Start();

            сервер = new Сервер();
            Чтение_настроек_сервера_с_диска();
            сервер.Старт();

            Console.ReadLine();
        }

        static void Работа_со_списками()
        {
            //Очистка списка событий
            DateTime время_последнего_события = DateTime.Now;
            while (true)
            {
                Thread.Sleep(new TimeSpan(0, 1, 0));
                if(список_событий.Count>0)
                    for (int i=0;i< список_событий.Count;)
                    {
                        if (i >= список_событий.Count) break;
                        if ((DateTime.Now - список_событий[i].время) > new TimeSpan(3, 0, 0, 0))
                        {
                            список_событий.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }
                
                if (список_событий.Last().время != время_последнего_события)
                {
                    Запись_событий_на_диск();
                }

                время_последнего_события = список_событий.Last().время;

                //Очистка списка сеансов
                if (список_сеансов.Count > 0)
                    for (int i = 0; i < список_сеансов.Count;)
                    {
                        if (i >= список_сеансов.Count) break;
                        if ((DateTime.Now - список_сеансов[i].время_последнего_запроса) > new TimeSpan(0, 20, 0))
                        {
                            Console.WriteLine($"Пользователь {список_сеансов[i].пользователь.логин} вышел из системы в связи с бездействием.");
                            список_событий.Add(new Событие(список_сеансов[i].пользователь.логин, $"Пользователь {список_сеансов[i].пользователь.логин} вышел из системы в связи с бездействием.", 2));
                            список_сеансов.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }


                //Перевод устройств в офлайн
                foreach (Устройство устройство in список_устройств)
                {
                    if (устройство.статус == "off") continue;
                    DateTime последний_выход_на_связь = new DateTime(2000, 1, 1, 0, 0, 0);
                    for (int i = список_событий.Count - 1; i >= 0; i--)
                    {
                        if (список_событий[i].пользователь == устройство.имя && список_событий[i].код == 40)
                        {
                            последний_выход_на_связь = список_событий[i].время;
                            break;
                        }
                    }
                    if ((DateTime.Now - последний_выход_на_связь) > new TimeSpan(0, 3, 0))
                    {
                        устройство.статус = "off";
                        Console.WriteLine($"Устройство {устройство.имя} изменило статус на ofline.");
                        список_событий.Add(new Событие(устройство.имя, $"Устройство {устройство.имя} изменило статус на ofline.", 41));
                    }
                }

                //Очистка списка ошибок
                if (DateTime.Now.Hour == 0 && (DateTime.Now.Minute == 0 || DateTime.Now.Minute == 1))
                {
                    список_ошибок.Clear();
                }
            }
        }

        static void Запись_пользователей_на_диск()
        {
            
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                DateFormatString = "dd.MM.yyyy HH:mm:ss"
            };
            string data = JsonConvert.SerializeObject(список_пользователей,serializerSettings);

            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            try
            {
                using (FileStream fstream = new FileStream($"{path}\\Users.bsv", FileMode.OpenOrCreate))
                {
                    // преобразуем строку в байты
                    byte[] array = System.Text.Encoding.Default.GetBytes(data);
                    // запись массива байтов в файл
                    fstream.Write(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при записи данных о пользователях\r\n"+e.Message);
                список_событий.Add(new Событие("Ошибка при записи данных о пользователях\r\n" + e.Message, 10));
                return;
            }

            Console.WriteLine("Список пользователей сохранен в файл.");
            список_событий.Add(new Событие("Список пользователей сохранен в файл.", 11));
        }

        static void Чтение_пользователей_с_диска()
        {
            
            string data = "";
            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                Console.WriteLine("Ошибка чтения файла Users.bsv.\r\nПапка Data не найдена.");
                список_событий.Add(new Событие("Ошибка чтения файла Users.bsv.\r\nПапка Data не найдена.", 12));
                return;
            }

            try
            {
                using (FileStream fstream = File.OpenRead($"{path}\\Users.bsv"))
                {
                    // преобразуем строку в байты
                    byte[] array = new byte[fstream.Length];
                    // считываем данные
                    fstream.Read(array, 0, array.Length);
                    // декодируем байты в строку
                    data = System.Text.Encoding.Default.GetString(array);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при чтении данных о пользователях\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при чтении данных о пользователях\r\n" + e.Message, 12));
                return;
            }

            Пользователь[] пользователи = JsonConvert.DeserializeObject<Пользователь[]>(data);
            список_пользователей.Clear();
            if (пользователи.Length > 0)
            {
                for (int i = 0; i < пользователи.Length; i++)
                    список_пользователей.Add(пользователи[i]);
            }
            
            Console.WriteLine("Список пользователей загружен из файла.");
            список_событий.Add(new Событие("Список пользователей загружен из файла.", 13));
        }

        static void Запись_устройств_на_диск()
        {

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                DateFormatString = "dd.MM.yyyy HH:mm:ss"
            };
            string data = JsonConvert.SerializeObject(список_устройств, serializerSettings);

            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            try
            {
                using (FileStream fstream = new FileStream($"{path}\\Devices.bsv", FileMode.OpenOrCreate))
                {
                    // преобразуем строку в байты
                    byte[] array = System.Text.Encoding.Default.GetBytes(data);
                    // запись массива байтов в файл
                    fstream.Write(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при записи данных о устройствах\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при записи данных о устройствах\r\n" + e.Message, 10));
                return;
            }

            Console.WriteLine("Список устройств сохранен в файл.");
            список_событий.Add(new Событие("Список устройств сохранен в файл.", 11));
        }

        static void Чтение_устройств_с_диска()
        {

            string data = "";
            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                Console.WriteLine("Ошибка чтения файла Devices.bsv.\r\nПапка Data не найдена.");
                список_событий.Add(new Событие("Ошибка чтения файла Devices.bsv.\r\nПапка Data не найдена.", 12));
                return;
            }

            try
            {
                using (FileStream fstream = File.OpenRead($"{path}\\Devices.bsv"))
                {
                    // преобразуем строку в байты
                    byte[] array = new byte[fstream.Length];
                    // считываем данные
                    fstream.Read(array, 0, array.Length);
                    // декодируем байты в строку
                    data = System.Text.Encoding.Default.GetString(array);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при чтении данных о устройствах\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при чтении данных о устройствах\r\n" + e.Message, 12));
                return;
            }

            Устройство[] устройства = JsonConvert.DeserializeObject<Устройство[]>(data);
            список_устройств.Clear();
            if (устройства.Length > 0)
            {
                for (int i = 0; i < устройства.Length; i++)
                    список_устройств.Add(устройства[i]);
            }

            Console.WriteLine("Список устройств загружен из файла.");
            список_событий.Add(new Событие("Список устройств загружен из файла.", 13));
        }

        static void Запись_событий_на_диск()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            };
            string data = JsonConvert.SerializeObject(список_событий, serializerSettings);

            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            try
            {
                using (FileStream fstream = new FileStream($"{path}\\Events.bsv", FileMode.OpenOrCreate))
                {
                    // преобразуем строку в байты
                    byte[] array = System.Text.Encoding.Default.GetBytes(data);
                    // запись массива байтов в файл
                    fstream.Write(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при записи данных о событиях\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при записи данных о событиях\r\n" + e.Message, 10));
                return;
            }

            Console.WriteLine("Список событий сохранен в файл.");
            список_событий.Add(new Событие("Список событий сохранен в файл.", 11));
        }

        static void Чтение_событий_с_диска()
        {
            string data = "";
            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                Console.WriteLine("Ошибка чтения файла Events.bsv.\r\nПапка Data не найдена.");
                список_событий.Add(new Событие("Ошибка чтения файла Events.bsv.\r\nПапка Data не найдена.", 12));
                return;
            }

            try
            {
                using (FileStream fstream = File.OpenRead($"{path}\\Events.bsv"))
                {
                    // преобразуем строку в байты
                    byte[] array = new byte[fstream.Length];
                    // считываем данные
                    fstream.Read(array, 0, array.Length);
                    // декодируем байты в строку
                    data = System.Text.Encoding.Default.GetString(array);
                }


                Событие[] события = JsonConvert.DeserializeObject<Событие[]>(data);
                список_событий.Clear();


                if (события.Length > 0)
                {
                    for (int i = 0; i < события.Length; i++)
                        список_событий.Add(события[i]);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при чтении данных о событиях\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при чтении данных о событиях\r\n" + e.Message, 12));
                return;
            }

            Console.WriteLine("Список событий загружен из файла.");
            список_событий.Add(new Событие("Список событий загружен из файла.", 13));
        }

        static void Запись_настроек_сервера_на_диск()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            };
            string data = JsonConvert.SerializeObject(сервер.настройки, serializerSettings);

            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            try
            {
                using (FileStream fstream = new FileStream($"{path}\\Server configuration.bsv", FileMode.OpenOrCreate))
                {
                    // преобразуем строку в байты
                    byte[] array = System.Text.Encoding.Default.GetBytes(data);
                    // запись массива байтов в файл
                    fstream.Write(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при записи настроек сервера.\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при записи настроек сервера.\r\n" + e.Message, 10));
                return;
            }

            Console.WriteLine("Настройки сервера сохранены в файл.");
            список_событий.Add(new Событие("Настройки сервера сохранены в файл.", 11));
        }

        static void Чтение_настроек_сервера_с_диска()
        {
            string data = "";
            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            {
                dirInfo.Create();
            }

            try
            {
                using (FileStream fstream = File.OpenRead($"{path}\\Server configuration.bsv"))
                {
                    // преобразуем строку в байты
                    byte[] array = new byte[fstream.Length];
                    // считываем данные
                    fstream.Read(array, 0, array.Length);
                    // декодируем байты в строку
                    data = System.Text.Encoding.Default.GetString(array);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при чтении настроек сервера\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при чтении настроек сервера\r\n" + e.Message, 12));
                Запись_настроек_сервера_на_диск();
                return;
            }

            сервер.настройки = JsonConvert.DeserializeObject<Сервер.Настройки>(data);
            Console.WriteLine("Настройки сервера загружены из файла.");
            список_событий.Add(new Событие("Настройки сервера загружены из файла.", 13));
        }

        static void Запись_скриптов_на_диск()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            };
            string data = JsonConvert.SerializeObject(список_скриптов, serializerSettings);

            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            try
            {
                using (FileStream fstream = new FileStream($"{path}\\Scripts.bsv", FileMode.OpenOrCreate))
                {
                    // преобразуем строку в байты
                    byte[] array = System.Text.Encoding.Default.GetBytes(data);
                    // запись массива байтов в файл
                    fstream.Write(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при записи данных о скриптах\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при записи данных о скриптах\r\n" + e.Message, 10));
                return;
            }

            Console.WriteLine("Список сскриптов сохранен в файл.");
            список_событий.Add(new Событие("Список скриптов сохранен в файл.", 11));
        }

        static void Чтение_скриптов_с_диска()
        {
            string data = "";
            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                Console.WriteLine("Ошибка чтения файла Scripts.bsv.\r\nПапка Data не найдена.");
                список_событий.Add(new Событие("Ошибка чтения фала Scripts.bsv.\r\nПапка Data не найдена.", 12));
                return;
            }

            try
            {
                using (FileStream fstream = File.OpenRead($"{path}\\Scripts.bsv"))
                {
                    // преобразуем строку в байты
                    byte[] array = new byte[fstream.Length];
                    // считываем данные
                    fstream.Read(array, 0, array.Length);
                    // декодируем байты в строку
                    data = System.Text.Encoding.Default.GetString(array);
                }


                Скрипт[] скрипты = JsonConvert.DeserializeObject<Скрипт[]>(data);
                список_скриптов.Clear();


                if (скрипты.Length > 0)
                {
                    for (int i = 0; i < скрипты.Length; i++)
                        список_скриптов.Add(скрипты[i]);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при чтении данных о скриптах\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при чтении данных о скриптах\r\n" + e.Message, 12));
                return;
            }

            Console.WriteLine("Список событий загружен из файла.");
            список_событий.Add(new Событие("Список событий загружен из файла.", 13));
        }

        public class Сервер
        {
            public bool флаг_работы;
            public HttpListener listener = new HttpListener();
            public Настройки настройки = new Настройки();
            string температура;

            public Сервер()
            {
                флаг_работы = true;
                настройки.адрес_сервера = "http://+:16018/";
                настройки.адрес_сервера_предсказаний = "http://194.213.117.99:4813";
                настройки.адрес_сервера_температуры = "http://api.openweathermap.org/data/2.5/";
                настройки.город = "Yekaterinburg";
                температура = "0";
            }

            public struct Настройки
            {
                public string адрес_сервера { set; get; }// = "http://+:16017/";
                public string адрес_сервера_предсказаний { set; get; }//= "http://194.213.117.99:4813";
                public string адрес_сервера_температуры { set; get; }// = "http://api.openweathermap.org/data/2.5/";
                public string город { set; get; }
            }

            public void Старт()
            {
                listener.Prefixes.Add(настройки.адрес_сервера);
                listener.Start();
                Thread поток_получения_температуры = new Thread(ОбновлениеТемпературы);
                поток_получения_температуры.IsBackground = true;
                поток_получения_температуры.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключений.");

                while (флаг_работы)
                {
                    HttpListenerContext context = listener.GetContext();
                    Thread.Sleep(10);
                    Thread НовыйПотокЗапроса = new Thread(ОбработкаЗапроса);
                    НовыйПотокЗапроса.IsBackground = true;
                    НовыйПотокЗапроса.Start(context);
                }
                listener.Close();
            }

            public async void ОбновлениеТемпературы()
            {
                while (флаг_работы)
                {
                    
                    HttpClient Клиент = new HttpClient();
                    HttpRequestMessage СообщениеЗапроса = new HttpRequestMessage();
                    СообщениеЗапроса.RequestUri = new Uri(настройки.адрес_сервера_температуры + $"find?q={настройки.город}&type=like&APPID=e9cb9eac3d32b1d896c100f05482ef3d");
                    
                    HttpResponseMessage Ответ = new HttpResponseMessage();
                    try
                    {
                        Ответ = await Клиент.SendAsync(СообщениеЗапроса);

                        Console.WriteLine($"Послан запрос на получение температуры на {настройки.адрес_сервера_температуры}");
                        список_событий.Add(new Событие($"Послан запрос на получение температуры на {настройки.адрес_сервера_температуры}", 20));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Не удалось получить ответ с {настройки.адрес_сервера_температуры}\r\n{e.Message}");
                        список_событий.Add(new Событие($"Не удалось получить ответ с {настройки.адрес_сервера_температуры}\r\n{e.Message}", 23));
                        температура = "0";
                    }

                    Console.WriteLine($"Получен ответ с сервера температуры");
                    список_событий.Add(new Событие($"Получен ответ с сервера температуры", 22));

                    HttpContent СодержимоеОтвета = Ответ.Content;
                    string ДанныеССервераТемпературы = await СодержимоеОтвета.ReadAsStringAsync();
                    
                    string ТемператураВКельвинах;
                    try
                    {
                        //ТемператураВКельвинах = ДанныеССервераТемпературы.Substring(ДанныеССервераТемпературы.IndexOf("\"temp\":") + 7, ДанныеССервераТемпературы.IndexOf(",\"pressure\"") - (ДанныеССервераТемпературы.IndexOf("\"temp\":") + 7) - 2).Replace(".", ",");
                        ТемператураВКельвинах = ДанныеССервераТемпературы.Substring(ДанныеССервераТемпературы.IndexOf("\"temp\":") + 7);
                        ТемператураВКельвинах = ТемператураВКельвинах.Remove(ТемператураВКельвинах.IndexOf("."));
                    }
                    catch
                    {
                        ТемператураВКельвинах = "272";
                    }
                    
                    double n;
                    if (double.TryParse(ТемператураВКельвинах, out n))
                        температура = (Convert.ToDouble(ТемператураВКельвинах) - 273.15 > 0 ? "+" : "") + ((int)(Convert.ToDouble(ТемператураВКельвинах) - 273.15)).ToString();
                    else температура = "0";


                    int int_n;

                    if (!int.TryParse(температура, out int_n))
                        температура = "111";
                    else if (Convert.ToInt32(температура) > 50 || Convert.ToInt32(температура) < -50)
                        температура = "222";
                    Thread.Sleep(900000);
                }
            }

            void ОбработкаЗапроса(object Context)
            {
                HttpListenerContext context = (HttpListenerContext)Context;
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string RawUrl = request.RawUrl.Replace("+", " ").Replace("%2C", ",");

                Console.WriteLine("Получен запрос "+RawUrl);
                список_событий.Add(new Событие("Получен запрос " + RawUrl, 30));

                switch (RawUrl.IndexOf("?") != -1 ? RawUrl.Remove(RawUrl.IndexOf("?")) : RawUrl)
                {
                    case "/getTabloData.php":
                        {
                            string логин = Из_строки_по_ключу(RawUrl, "_user_");
                            string серийный_номер = Из_строки_по_ключу(RawUrl, "_sn_");
                            string пароль = Из_строки_по_ключу(RawUrl, "_password_");

                            Устройство устройство = null;
                            СписокОшибокУстройства данные_об_ошибках = null;

                            if (логин != "")
                            {
                                foreach (Устройство device in список_устройств)
                                {
                                    if (device.логин == логин)
                                    {
                                        устройство = device;
                                        break;
                                    }
                                }
                            }
                            else if (серийный_номер != "")
                            {
                                foreach (Устройство device in список_устройств)
                                {
                                    if (device.серийный_номер == серийный_номер)
                                    {
                                        устройство = device;
                                        break;
                                    }
                                }
                            }

                            if (устройство == null)
                            {
                                Console.WriteLine("Ошибка обработки запроса - устройство не распознано.");
                                список_событий.Add(new Событие("Ошибка обработки запроса - устройство не распознано.", 31));

                                string responseString = "<HTML><BODY> Error 401!</BODY></HTML>";
                                response.StatusCode = 401;
                                Отправка_ответа_на_запрос(response, responseString, RawUrl);
                                return;
                            }

                            if (устройство.пароль != пароль)
                            {
                                Console.WriteLine("Ошибка обработки запроса - пароль не совпадает.");
                                список_событий.Add(new Событие("Ошибка обработки запроса - пароль не совпадает.", 31));

                                string responseString = "<HTML><BODY> Error 401!</BODY></HTML>";
                                response.StatusCode = 401;
                                Отправка_ответа_на_запрос(response, responseString, RawUrl);
                                return;
                            }

                            Console.WriteLine($"Устройство {устройство.имя} вышло на связь.");
                            список_событий.Add(new Событие(устройство.имя,$"Устройство {устройство.имя} вышло на связь.", 40));
                            устройство.статус = "ok";

                            foreach (СписокОшибокУстройства список in список_ошибок)
                            {
                                if (список.серийный_номер == устройство.серийный_номер)
                                {
                                    данные_об_ошибках = список;
                                    break;
                                }
                            }

                            if (данные_об_ошибках == null)
                            {
                                данные_об_ошибках = new СписокОшибокУстройства(устройство.серийный_номер);
                                список_ошибок.Add(данные_об_ошибках);
                            }

                            данные_об_ошибках.количество_запросов++;

                            string статус = Из_строки_по_ключу(RawUrl, "_status_");

                            if (статус != "")
                            {
                                данные_об_ошибках.код_последней_ошибки= статус.Split(' ').
                                            Where(x => !string.IsNullOrWhiteSpace(x)).
                                            Select(x => int.Parse(x)).ToArray();
                            }

                            if (данные_об_ошибках.код_последней_ошибки[0] != 0)
                            {
                                данные_об_ошибках.количество_ошибок++;
                                данные_об_ошибках.коды_ошибок[данные_об_ошибках.код_последней_ошибки[0]]++;
                                устройство.статус = "err";
                                Console.WriteLine($"Устройство {устройство.имя} " +
                                    $"сообщило об ошибке {данные_об_ошибках.код_последней_ошибки[0]} " +
                                    $"{данные_об_ошибках.код_последней_ошибки[1]} " +
                                    $"{данные_об_ошибках.код_последней_ошибки[2]}.");
                                список_событий.Add(new Событие(устройство.имя,
                                   $"Устройство {устройство.имя} " +
                                    $"сообщило об ошибке {данные_об_ошибках.код_последней_ошибки[0]} " +
                                    $"{данные_об_ошибках.код_последней_ошибки[1]} " +
                                    $"{данные_об_ошибках.код_последней_ошибки[2]}.", 42));
                            }

                            string температура_устройства = Из_строки_по_ключу(RawUrl, "_temp_");
                            switch (температура_устройства)
                            {
                                default:
                                    {
                                        данные_об_ошибках.температура_устройства = температура_устройства;
                                        break;
                                    }
                                case "":
                                    {
                                        данные_об_ошибках.температура_устройства = "Нет информации.";
                                        break;
                                    }
                                case "100":
                                    {
                                        данные_об_ошибках.температура_устройства = "Нет датчика.";
                                        break;
                                    }
                            }



                            return;
                        }
                    default:
                    case "/":
                        {
                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                            return;
                        }
                    case "/login":
                        {
                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");
                            string пароль_из_запроса = Из_строки_по_ключу(RawUrl, "password");

                            Пользователь пользователь = new Пользователь();
                            foreach (Пользователь user in список_пользователей)
                            {
                                if (user.логин == логин_из_запроса)
                                {
                                    пользователь = user;
                                    break;
                                }
                            }
                            if (пользователь.логин == "" || пользователь.пароль != пароль_из_запроса)
                            {
                                Cookie cookie2 = new Cookie("err", $"1+{логин_из_запроса}");
                                cookie2.Expires = DateTime.Now + new TimeSpan(0, 0, 5);
                                response.SetCookie(cookie2);

                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            bool есть_сеанс = false;
                            foreach (Сеанс сеанс_в_списке in список_сеансов)
                            {
                                if (сеанс_в_списке.пользователь.Equals(пользователь))
                                {
                                    есть_сеанс = true;
                                    break;
                                }
                            }

                            if (есть_сеанс)
                            {
                                Cookie cookie2 = new Cookie("err", $"2+{логин_из_запроса}");
                                cookie2.Expires = DateTime.Now + new TimeSpan(0, 0, 5);
                                response.SetCookie(cookie2);

                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = new Сеанс(пользователь);
                            список_сеансов.Add(сеанс);

                            List<string[]> лист_куки = new List<string[]>();
                            лист_куки.Add(new string[2] { "seans_key", сеанс.код_сеанса.ToString() });
                            лист_куки.Add(new string[2] { "user_login", пользователь.логин });
                            лист_куки.Add(new string[2] { "user_class", пользователь.класс });
                            лист_куки.Add(new string[2] { "server_ip", request.Url.ToString().Remove(request.Url.ToString().IndexOf("16018") + 5) });

                            Cookie cookie = new Cookie("data", Набор_куки(лист_куки));
                            cookie.Expires = DateTime.Now + new TimeSpan(0, 0, 5);
                            response.SetCookie(cookie);
                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Главная.html"), RawUrl);
                            Console.WriteLine($"Пользователь {пользователь.логин} вошел в систему.");
                            список_событий.Add(new Событие(пользователь.логин,$"Пользователь {пользователь.логин} вошел в систему.", 1));
                            return;
                        }
                    case "/end_seans":
                        {
                            if (!Проверка_логина_и_кода_сеанса(RawUrl))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");
                            список_сеансов.Remove(Сеанс_из_запроса(RawUrl));
                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                            Console.WriteLine($"Пользователь {логин_из_запроса} вышел из системы.");
                            список_событий.Add(new Событие(логин_из_запроса, $"Пользователь {логин_из_запроса} вышел из системы.", 1));
                            return;
                        }
                    case "/get_tablo_status_counts":
                        {
                            if (!Проверка_логина_и_кода_сеанса(RawUrl)) return;

                            Сеанс сеанс = Сеанс_из_запроса(RawUrl);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            int количество_устройств_ok = 0;
                            int количество_устройств_err = 0;
                            int количество_устройств_off = 0;
                            foreach (Устройство устройство in список_устройств)
                            {
                                switch (устройство.статус)
                                {
                                    case "ok":
                                        количество_устройств_ok++;
                                        break;
                                    case "err":
                                        количество_устройств_err++;
                                        break;
                                    case "off":
                                        количество_устройств_off++;
                                        break;
                                }
                            }
                            Отправка_ответа_на_запрос(response, ""+количество_устройств_ok+";"+ количество_устройств_err+";"+ количество_устройств_off, RawUrl);
                            return;
                        }
                    case "/get_tablo_datas":
                        {
                            if (!Проверка_логина_и_кода_сеанса(RawUrl)) return;

                            Сеанс сеанс = Сеанс_из_запроса(RawUrl);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            List<ДанныеУстройстваДляВеб> данные = new List<ДанныеУстройстваДляВеб>();
                            foreach (Устройство устройство in список_устройств)
                            {
                                ДанныеУстройстваДляВеб данныеУстройства = new ДанныеУстройстваДляВеб(устройство);
                                данные.Add(данныеУстройства);
                            }

                            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                            {
                                Formatting = Formatting.Indented,
                                DateFormatString = "dd.MM.yyyy HH:mm:ss"
                            };
                            string data = JsonConvert.SerializeObject(данные, serializerSettings);
                            Отправка_ответа_на_запрос(response, data, RawUrl);
                            return;
                        }
                    case "/test":
                        {
                            Console.WriteLine("123");
                            Отправка_ответа_на_запрос(response, "OK", RawUrl);
                            return;
                        }
                }
            }

            static void Отправка_ответа_на_запрос(HttpListenerResponse response, string responseString,string RawUrl)
            {
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.StatusCode = 200;
                response.StatusDescription = "OK";
                response.ContentType = "text/html";
                response.AddHeader("Access-Control-Allow-Origin", "*");
                try
                {
                    Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                    Console.WriteLine("Отправлен ответ на запрос " + RawUrl);
                    список_событий.Add(new Событие("Отправлен ответ на запрос " + RawUrl, 32));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Ошибка отправки ответа.\r\n " + e.Message);
                    список_событий.Add(new Событие("Ошибка отправки ответа.\r\n " + e.Message, 33));
                }
                response.Close();
                return;
            }

            static string Загрузка_страницы(string name)
            {
                string data = "";
                string path = $"{Environment.CurrentDirectory}\\Web";
                DirectoryInfo dirInfo = new DirectoryInfo(path);
                if (!dirInfo.Exists)
                {
                    Console.WriteLine($"Ошибка чтения фала {name}.\r\nПапка Web не найдена.");
                    список_событий.Add(new Событие($"Ошибка чтения фала{name}.\r\nПапка Web не найдена.", 12));
                    return data;
                }

                try
                {
                    using (FileStream fstream = File.OpenRead($"{path}\\{name}"))
                    {
                        // преобразуем строку в байты
                        byte[] array = new byte[fstream.Length];
                        // считываем данные
                        fstream.Read(array, 0, array.Length);
                        // декодируем байты в строку
                        data = System.Text.Encoding.UTF8.GetString(array);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Ошибка при чтении файла {name}\r\n" + e.Message);
                    список_событий.Add(new Событие($"Ошибка при чтении файла {name}\r\n" + e.Message, 12));
                    return data;
                }
                return data;
            }

            static string Из_строки_по_ключу(string строка, string ключ)
            {
                строка = HttpUtility.UrlDecode(строка);
                if (строка.IndexOf("?") == -1)
                    return "";
                строка = строка.Substring(строка.IndexOf("?") + 1);
                if (строка.IndexOf(ключ) == -1)
                    return "";
                строка = строка.Substring(строка.IndexOf(ключ) + ключ.Length + 1);
                if (строка.IndexOf("&") == -1)
                    return строка;
                строка = строка.Remove(строка.IndexOf("&"));
                return строка;
            }

            static string Набор_куки(List<string[]> лист)
            {
                string data = "";
                foreach (string[] строки in лист)
                {
                    data += строки[0] + "+=+" + строки[1] + "+||+"; 
                }
                if (data.IndexOf("+||+") != -1)
                    data = data.Remove(data.LastIndexOf("+||+"));
                return data;
            }

            static bool Проверка_логина_и_кода_сеанса(string RawUrl)
            {
                string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");
                string номер_сеанса_из_запроса = Из_строки_по_ключу(RawUrl, "seans_key");
                if (!int.TryParse(номер_сеанса_из_запроса, out int n))
                {
                    Console.WriteLine($"Ошибка обработки запроса. Номер сеанса не распознан. Обработка запроса прекращена.");
                    список_событий.Add(new Событие($"Ошибка обработки запроса. Номер сеанса не распознан. Обработка запроса прекращена.", 31));
                    return false;
                }

                bool сеанс_распознан = false;
                Сеанс сеанс = null;
                foreach (Сеанс seans in список_сеансов)
                {
                    if (seans.код_сеанса == int.Parse(номер_сеанса_из_запроса))
                    {
                        сеанс = seans;
                        сеанс_распознан = true;
                        break;
                    }
                }

                if (!сеанс_распознан)
                {
                    Console.WriteLine($"Ошибка обработки запроса. Сеанс не распознан. Обработка запроса прекращена.");
                    список_событий.Add(new Событие($"Ошибка обработки запроса. Сеанс не распознан. Обработка запроса прекращена.", 31));
                    return false;
                }

                if (сеанс.пользователь.логин != логин_из_запроса)
                {
                    Console.WriteLine($"Ошибка обработки запроса. Логин в запросе не совпадает с логином в активном сеансе. Обработка запроса прекращена.");
                    список_событий.Add(new Событие($"Ошибка обработки запроса. Логин в запросе не совпадает с логином в активном сеансе. Обработка запроса прекращена.", 31));
                    return false;
                }
                return true;
            }

            static Сеанс Сеанс_из_запроса(string RawUrl)
            {
                Сеанс сеанс = null;
                string ключ_из_запроса = Из_строки_по_ключу(RawUrl, "seans_key");

                if (!int.TryParse(ключ_из_запроса, out int n)) return сеанс;


                foreach (Сеанс s in список_сеансов)
                {
                    if (s.код_сеанса == Int32.Parse(ключ_из_запроса))
                    {
                        сеанс = s;
                        break;
                    }
                }

                return сеанс;
            }
        }

        class Пользователь
        {
            public string логин { get; set; }
            public string пароль { get; set; }
            public string класс { get; set; }

            public Пользователь()
            {
                логин = "";
                пароль = "";
                класс = "";
            }

            public string ToJSON()
            {
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    DateFormatString = "dd.MM.yyyy HH:mm:ss"
                };
                return JsonConvert.SerializeObject(this, serializerSettings);
            }
        }

        class Событие
        {
            public DateTime время { get; set; }
            public string текст { get; set; }
            public int код { get; set; }
            public string пользователь { get; set; }

            public Событие()
            { }

            public Событие(string текст, int код)
            {
                время = DateTime.Now;
                this.текст = текст;
                this.код = код;
                пользователь = "Система";
            }

            public Событие(string пользователь, string текст, int код)
            {
                время = DateTime.Now;
                this.текст = текст;
                this.код = код;
                this.пользователь = пользователь;
            }

            //коды событий
            //1-пользователь вошел
            //2-пользователь вышел
            //10-ошибка записи файла
            //11-запись файла
            //12-ошибка чтения файла
            //13-чтение файла
            //20-отправлен запрос
            //21-ошибка отправления запроса
            //22-получен ответ
            //23-не получен ответ
            //30-получен запрос
            //31-ошибка получения запроса
            //32-отправлен ответ
            //33-ошибка отправления ответа
            //40-устройстов вышло на связь
            //41-устройство перешло в офлайн
            //42-устройство сообщило об ошибке
        }

        class Сеанс
        {
            public Пользователь пользователь;
            public int код_сеанса;
            public DateTime время_последнего_запроса;

            public Сеанс(Пользователь пользователь)
            {
                this.пользователь = пользователь;
                Random random = new Random(DateTime.Now.Millisecond);
                код_сеанса = random.Next() % 1000000 + 1;
                время_последнего_запроса = DateTime.Now;
            }
        }

        class Устройство
        {
            public string серийный_номер { set; get; }
            public string имя { set; get; }
            public string логин { set; get; }
            public string пароль { get; set; }
            public string статус { get; set; }
            public string адрес { get; set; }
            public string телефон { get; set; }
            public double широта { get; set; }
            public double долгота { get; set; }
            public string тип_сообщения { get; set; }
            public string сообщение { get; set; }

            public Устройство(string серийный_номер)
            {
                this.серийный_номер = серийный_номер;
                имя = "";
                логин = "";
                пароль = "";
                статус = "off";
                адрес = "";
                телефон = "";
                тип_сообщения = "Не выводится";
                сообщение = "";
            }
        }

        class ДанныеУстройстваДляВеб
        {
            public string серийный_номер { set; get; }
            public string логин { get; set; }
            public string имя { set; get; }
            public string адрес { set; get; }
            public string тип_сообщения { set; get; }
            public string статус { set; get; }
            public double широта { get; set; }
            public double долгота { get; set; }

            public ДанныеУстройстваДляВеб(Устройство устройство)
            {
                this.серийный_номер = устройство.серийный_номер;
                this.логин = устройство.логин;
                this.имя = устройство.имя;
                this.адрес = устройство.адрес;
                this.тип_сообщения = устройство.тип_сообщения;
                this.статус = устройство.статус;
                this.широта = устройство.широта;
                this.долгота = устройство.долгота;
            }
        }

        class СписокОшибокУстройства
        {
            public string серийный_номер;
            public int количество_запросов;
            public int количество_ошибок;
            public int[] код_последней_ошибки=new int[3];
            public int[] коды_ошибок=new int[256];
            public string температура_устройства;

            public СписокОшибокУстройства(string серийный_номер)
            {
                this.серийный_номер = серийный_номер;
                for (int i = 0; i < 3; i++) код_последней_ошибки[i] = 0;
                for (int i = 0; i < 256; i++) коды_ошибок[i] = 0;
                количество_запросов = 0;
                количество_ошибок = 0;
                температура_устройства = "Нет информации.";
            }
        }

        class Скрипт
        {
            string имя;
            public string код;

        }
    }
}
