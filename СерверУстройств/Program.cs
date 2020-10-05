﻿using System;
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
using System.Xml;
using System.Xml.Linq;

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
        static List<СообщениеЕДДС> список_сообщенийЕДДС = new List<СообщениеЕДДС>();
        static Сервер сервер;
        static lastreq[] lr = new lastreq[2];

        struct lastreq
        {
            public DateTime time { set; get; }
            public string Url { set; get; }
        }

        static void Main(string[] args)
        {
            lr[0].time = DateTime.Now;
            lr[0].Url = "";
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
            /*Скрипт скрипт = new Скрипт();
            скрипт.имя = "Стандарт 2х3";
            скрипт.код = "###тип_скрипта{стандарт}\r\n" +
                "###количество_строк{4}\r\n" +
                "###размер_строки_по_вертикали{11}\r\n" +
                "###шрифт1{s}\r\n" +
                "###шрифт2{s}\r\n";
            список_скриптов.Add(скрипт);
            Запись_скриптов_на_диск();*/

            Чтение_устройств_с_диска();
            //Запись_устройств_на_диск();
            Чтение_событий_с_диска();
            Чтение_пользователей_с_диска();
            Чтение_скриптов_с_диска();

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
                            список_сеансов[i].пользователь.онлайн = false;
                            список_сеансов.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }


                //Перевод устройств в офлайн и изменение типа выводимого сообщения
                foreach (Устройство устройство in список_устройств)
                {
                    if (устройство.тип_сообщения == "Индивидуальное" && устройство.время_показа_индивидуального_сообщения > DateTime.Now)
                        устройство.тип_сообщения = "По умолчанию";

                    if (устройство.тип_сообщения == "По умолчанию" && (устройство.сообщение_по_умолчанию == "" || устройство.сообщение_по_умолчанию == null))
                        устройство.тип_сообщения = "Не выводится";

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
                Formatting = Newtonsoft.Json.Formatting.Indented,
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
                Formatting = Newtonsoft.Json.Formatting.Indented,
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
                Formatting = Newtonsoft.Json.Formatting.Indented,
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
                using (FileStream fstream = new FileStream($"{path}\\Events1.bsv", FileMode.OpenOrCreate))
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

            try
            {
                using (FileStream fstream = File.OpenRead($"{path}\\Events1.bsv"))
                {
                    // преобразуем строку в байты
                    byte[] array = new byte[fstream.Length];
                    // считываем данные
                    fstream.Read(array, 0, array.Length);
                    // декодируем байты в строку
                    data = System.Text.Encoding.Default.GetString(array);
                }

                Событие[] события = JsonConvert.DeserializeObject<Событие[]>(data);
            }
            catch
            {
                Запись_событий_на_диск();
            }
            File.Delete($"{path}\\Events.bsv");
            File.Move($"{path}\\Events1.bsv", $"{path}\\Events.bsv");

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
                Formatting = Newtonsoft.Json.Formatting.Indented,
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
                Formatting = Newtonsoft.Json.Formatting.Indented,
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

        static void Запись_сообщений_на_диск()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
            };
            string data = JsonConvert.SerializeObject(список_сообщенийЕДДС, serializerSettings);

            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            try
            {
                using (FileStream fstream = new FileStream($"{path}\\Messages.bsv", FileMode.OpenOrCreate))
                {
                    // преобразуем строку в байты
                    byte[] array = System.Text.Encoding.Default.GetBytes(data);
                    // запись массива байтов в файл
                    fstream.Write(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при записи данных о сообщениях\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при записи данных о сообщений\r\n" + e.Message, 10));
                return;
            }

            Console.WriteLine("Список сообщений сохранен в файл.");
            список_событий.Add(new Событие("Список сообщений сохранен в файл.", 11));
        }

        static void Чтение_сообщений_с_диска()
        {
            string data = "";
            string path = $"{Environment.CurrentDirectory}\\Data";
            DirectoryInfo dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                Console.WriteLine("Ошибка чтения файла Messages.bsv.\r\nПапка Data не найдена.");
                список_событий.Add(new Событие("Ошибка чтения фала Messages.bsv.\r\nПапка Data не найдена.", 12));
                return;
            }

            try
            {
                using (FileStream fstream = File.OpenRead($"{path}\\Messages.bsv"))
                {
                    // преобразуем строку в байты
                    byte[] array = new byte[fstream.Length];
                    // считываем данные
                    fstream.Read(array, 0, array.Length);
                    // декодируем байты в строку
                    data = System.Text.Encoding.Default.GetString(array);
                }


                СообщениеЕДДС[] сообщения = JsonConvert.DeserializeObject<СообщениеЕДДС[]>(data);
                список_скриптов.Clear();


                if (сообщения.Length > 0)
                {
                    for (int i = 0; i < сообщения.Length; i++)
                        список_сообщенийЕДДС.Add(сообщения[i]);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка при чтении данных о сообщениях\r\n" + e.Message);
                список_событий.Add(new Событие("Ошибка при чтении данных о сообщениях\r\n" + e.Message, 12));
                return;
            }

            Console.WriteLine("Список сообщений загружен из файла.");
            список_событий.Add(new Событие("Список сообщений загружен из файла.", 13));
        }

        public class Сервер
        {
            public bool флаг_работы;
            public HttpListener listener = new HttpListener();
            public Настройки настройки = new Настройки();
            public string температура;

            public Сервер()
            {
                флаг_работы = true;
                настройки.адрес_сервера = "http://+:16017/";
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

            public UInt16 CRC(byte[] buf, int len)
            {
                UInt16 crc = 0xFFFF;

                for (int pos = 0; pos < len; pos++)
                {
                    crc ^= (UInt16)buf[pos];

                    for (int i = 8; i != 0; i--)
                    {
                        if ((crc & 0x0001) != 0)
                        {
                            crc >>= 1;
                            crc ^= 0xA001;
                        }
                        else
                            crc >>= 1;
                    }
                }

                return crc;
            }

            void ОбработкаЗапроса(object Context)
            {
                HttpListenerContext context = (HttpListenerContext)Context;
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string RawUrl = request.RawUrl.Replace("+", " ").Replace("%2C", ",");
                lr[1] = lr[0];
                lr[0].time = DateTime.Now;
                lr[0].Url = RawUrl;

                Console.WriteLine("Получен запрос "+RawUrl);
                список_событий.Add(new Событие("Получен запрос " + RawUrl, 30));

                switch (RawUrl.IndexOf("?") != -1 ? RawUrl.Remove(RawUrl.IndexOf("?")) : RawUrl)
                {
                    case "/setStatus":
                        {
                            string серийный_номер = Из_строки_по_ключу(RawUrl, "_sn_");

                            Устройство устройство = null;

                            if (серийный_номер != "")
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

                            Console.WriteLine($"Устройство {устройство.имя} вышло на связь.");
                            список_событий.Add(new Событие(устройство.имя, $"Устройство {устройство.имя} вышло на связь.", 40));
                            устройство.статус = "ok";

                            Отправка_ответа_на_запрос(response, "Ok", RawUrl);

                            return;
                        }
                    case "/getTabloData":
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

                            if (пароль!=""&&устройство.пароль != пароль)
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
                            int[] код_ошибки = new int[3] { 0, 0, 0 };
                            if (статус != "")
                            {
                                
                                код_ошибки = статус.Split(' ').
                                            Where(x => !string.IsNullOrWhiteSpace(x)).
                                            Select(x => int.Parse(x)).ToArray();
                            }

                            if (код_ошибки[0] != 0)
                            {
                                данные_об_ошибках.код_последней_ошибки = код_ошибки;
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

                            Скрипт скрипт = null;

                            foreach (Скрипт с in список_скриптов)
                            {
                                if (с.имя == устройство.имя_скрипта)
                                {
                                    скрипт = с;
                                    break;
                                }
                            }

                            if (скрипт == null)
                            {
                                Console.WriteLine($"Для устройства {устройство.имя} не найден скрипт с именем \"{устройство.имя_скрипта}\".");
                                список_событий.Add(new Событие(устройство.имя, $"Для устройства {устройство.имя} не найден скрипт с именем \"{устройство.имя_скрипта}\".", 33));
                                return;
                            }

                            string текст_скрипта = скрипт.Нужна_обработка() ? скрипт.Обработка(устройство) : скрипт.код;

                            byte[] ДлинаСкрипта = { 0x00, 0x00, 0x00, 0x00 };
                            ДлинаСкрипта = BitConverter.GetBytes(Encoding.Default.GetBytes(текст_скрипта).Length + 1);

                            response.ContentLength64 = Encoding.Default.GetBytes(текст_скрипта).Length + 13;
                            byte[] buffer = Encoding.Default.GetBytes($"%%%{устройство.код_обновления_скрипта}");

                            try
                            {
                                Stream output = response.OutputStream;
                                output.Write(buffer, 0, buffer.Length);

                                byte[] bufCRC = new byte[] { 0x01, 0x20 };
                                bufCRC = bufCRC.Concat(ДлинаСкрипта).ToArray();
                                bufCRC = bufCRC.Concat(new byte[] { 0x12 }).ToArray();
                                bufCRC = bufCRC.Concat(Encoding.Default.GetBytes(текст_скрипта)).ToArray();
                                output.Write(bufCRC, 0, bufCRC.Length);

                                UInt16 CRC16 = CRC(bufCRC, bufCRC.Length);
                                buffer = new byte[] { (byte)(CRC16), (byte)(CRC16 >> 8) };
                                output.Write(buffer, 0, buffer.Length);
                                output.Close();

                                Console.WriteLine($"Отправлен ответ на запрос устройству \"{устройство.имя}\".");
                                список_событий.Add(new Событие(устройство.имя, $"Отправлен ответ на запрос устройству \"{устройство.имя}\".", 32));
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Ошибка обработки запроса от устройства \"{устройство.имя}\"."+e.Message);
                                список_событий.Add(new Событие(устройство.имя, $"Ошибка обработки запроса от устройства \"{устройство.имя}\"." + e.Message, 33));
                            }

                            if (устройство.код_обновления_скрипта != 0)
                            {
                                устройство.код_обновления_скрипта = 0;
                                Запись_устройств_на_диск();
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
                                Cookie cookie = new Cookie("err", $"1+{логин_из_запроса}");
                                cookie.Expires = DateTime.Now + new TimeSpan(0, 0, 2);
                                response.SetCookie(cookie);

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
                            bool lr_совпадает = false;
                            if (lr[0].Url == lr[1].Url && (lr[0].time - lr[1].time) < new TimeSpan(0, 0, 2)) lr_совпадает = true;

                            if (есть_сеанс&&!lr_совпадает)
                            {
                                Cookie cookie = new Cookie("err", $"2+{логин_из_запроса}");
                                cookie.Expires = DateTime.Now + new TimeSpan(0, 0, 5);
                                response.SetCookie(cookie);

                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = new Сеанс(пользователь);
                            список_сеансов.Add(сеанс);

                            string адрес_переадресации = Выбор_адреса_в_зависимости_от_класса_пользователя(пользователь);

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={пользователь.логин}");

                            Отправка_ответа_на_запрос(response, Переадресация(0,null, request.Url.ToString().Remove(request.Url.ToString().IndexOf("/")) + адрес_переадресации), RawUrl);
                            Console.WriteLine($"Пользователь {пользователь.логин} вошел в систему.");
                            список_событий.Add(new Событие(пользователь.логин,$"Пользователь {пользователь.логин} вошел в систему.", 1));
                            пользователь.онлайн = true;
                            return;
                        }
                    case "/devices":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            сеанс.время_последнего_запроса = DateTime.Now;
                            Пользователь пользователь = сеанс.пользователь;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string адрес_переадресации = Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь);

                            Отправка_ответа_на_запрос(response, Переадресация(0, null, request.Url.ToString().Remove(request.Url.ToString().IndexOf("/")) + адрес_переадресации), RawUrl);
                            return;
                        }
                    case "/main_devices_admin":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            сеанс.время_последнего_запроса = DateTime.Now;
                            Пользователь пользователь = сеанс.пользователь;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Главная_устройства_администратор.html"), RawUrl);
                            return;
                        }
                    case "/main_devices_cod":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            сеанс.время_последнего_запроса = DateTime.Now;
                            Пользователь пользователь = сеанс.пользователь;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Главная_устройства_цод.html"), RawUrl);
                            return;
                        }
                    case "/end_seans":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            for (int i = 0; i < список_сеансов.Count; i++)
                            {
                                if (список_сеансов[i].код_сеанса == сеанс.код_сеанса)
                                {
                                    список_сеансов.RemoveAt(i);
                                    break;
                                }
                            }
                            Отправка_ответа_на_запрос(response, Переадресация(0,null, request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/"))), RawUrl);
                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} вышел из системы.");
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} вышел из системы.", 1));
                            return;
                        }
                    case "/get_tablo_status_counts":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;

                            Сеанс сеанс = Сеанс_из_куки(request);
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
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;
                            string серийный_номер_из_запроса = Из_строки_по_ключу(RawUrl, "_snd_");

                            List<ДанныеУстройстваДляВеб> данные = new List<ДанныеУстройстваДляВеб>();

                            if (серийный_номер_из_запроса == "")
                            {
                                foreach (Устройство устройство in список_устройств)
                                {
                                    ДанныеУстройстваДляВеб данныеУстройства = new ДанныеУстройстваДляВеб(устройство);
                                    данные.Add(данныеУстройства);
                                }
                            }
                            else
                            {
                                Устройство устройство = null;
                                foreach (Устройство dev in список_устройств)
                                {
                                    if (dev.серийный_номер == серийный_номер_из_запроса)
                                    {
                                        устройство = dev;
                                        break;
                                    }
                                }
                                if (устройство != null)
                                {
                                    ДанныеУстройстваДляВеб данныеУстройства = new ДанныеУстройстваДляВеб(устройство);
                                    данные.Add(данныеУстройства);
                                }
                            }

                            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                            {
                                Formatting = Newtonsoft.Json.Formatting.Indented,
                                DateFormatString = "dd.MM.yyyy HH:mm:ss"
                            };
                            string data = JsonConvert.SerializeObject(данные, serializerSettings);
                            Отправка_ответа_на_запрос(response, data, RawUrl);
                            return;
                        }
                    case "/get_errors_data":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;
                            string серийный_номер_из_запроса = Из_строки_по_ключу(RawUrl, "_snd_");

                            СписокОшибокУстройства списокОшибок = null;
                            foreach (СписокОшибокУстройства список in список_ошибок)
                            {
                                if (список.серийный_номер == серийный_номер_из_запроса)
                                {
                                    списокОшибок = список;
                                    break;
                                }
                            }

                            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                            {
                                Formatting = Newtonsoft.Json.Formatting.Indented,
                                DateFormatString = "dd.MM.yyyy HH:mm:ss"
                            };
                            string data = JsonConvert.SerializeObject(списокОшибок, serializerSettings);
                            Отправка_ответа_на_запрос(response, data, RawUrl);
                            return;
                        }
                    case "/change_messages":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            string сообщение = Из_строки_по_ключу(RawUrl, "string");
                            string время_строкой = Из_строки_по_ключу(RawUrl, "created");
                            DateTime time = new DateTime();
                            DateTime время = DateTime.TryParse(время_строкой, out time) ? DateTime.Parse(время_строкой) : new DateTime(1,1,1);
                            string временный_список_измененных_устройств = "";
                            foreach (Устройство устройство in список_устройств)
                            {
                                if (Из_строки_по_ключу(RawUrl, "_snd_" + устройство.серийный_номер) == "true")
                                {
                                    устройство.сообщение_индивидуальное = сообщение;
                                    устройство.время_показа_индивидуального_сообщения = время;
                                    временный_список_измененных_устройств += ", \r\n\t\t" + устройство.имя;
                                }
                            }

                            string адрес_переадресации = Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь);

                            Отправка_ответа_на_запрос(response, Переадресация(0,null, request.Url.ToString().Remove(request.Url.ToString().IndexOf("/")) + адрес_переадресации), RawUrl);
                            Запись_устройств_на_диск();
                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} изменил индивидуальное сообщение в следующих устройствах:"+временный_список_измененных_устройств);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} изменил индивидуальное сообщение в следующих устройствах:" + временный_список_измененных_устройств, 3));
                            break;
                        }
                    case "/device":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string серийный_номер_из_запроса = Из_строки_по_ключу(RawUrl, "_snd_");

                            Устройство устройство = null;
                            foreach (Устройство dev in список_устройств)
                            {
                                if (dev.серийный_номер == серийный_номер_из_запроса)
                                {
                                    устройство = dev;
                                    break;
                                }
                            }

                            if (устройство == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Внутренняя ошибка. Устройство с серийным номером {серийный_номер_из_запроса} не найдено.",
                                    request.Url.ToString().Remove(request.Url.ToString().IndexOf("/")) + Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь)), RawUrl);
                                return;
                            }

                            response.AppendHeader("Set-Cookie", $"data={устройство.серийный_номер}; Max-age=5");
                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Страница_устройства.html"), RawUrl);
                            return;
                        }
                    case "/change_device":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string серийный_номер_из_запроса = Из_строки_по_ключу(RawUrl, "serial_number");

                            Устройство устройство = null;
                            foreach (Устройство dev in список_устройств)
                            {
                                if (dev.серийный_номер == серийный_номер_из_запроса)
                                {
                                    устройство = dev;
                                    break;
                                }
                            }

                            if (устройство == null)
                            {
                                 Отправка_ответа_на_запрос(response, Переадресация(5, $"Внутренняя ошибка. Устройство с серийным номером {серийный_номер_из_запроса} не найдено.",
                                     request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/change_device?")) +
                                     Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь)), RawUrl);
                                return;
                            }

                            устройство.имя = Из_строки_по_ключу(RawUrl,"name");
                            устройство.логин = Из_строки_по_ключу(RawUrl, "login");
                            устройство.пароль = Из_строки_по_ключу(RawUrl, "password");
                            устройство.телефон = Из_строки_по_ключу(RawUrl, "phone");
                            устройство.адрес = Из_строки_по_ключу(RawUrl, "address");
                            double temp;
                            устройство.широта = double.TryParse(Из_строки_по_ключу(RawUrl, "latitude").Replace(".",","),out temp)?double.Parse(Из_строки_по_ключу(RawUrl, "latitude").Replace(".", ",")) : 56.838607;
                            устройство.долгота = double.TryParse(Из_строки_по_ключу(RawUrl, "longitude").Replace(".", ","), out temp) ? double.Parse(Из_строки_по_ключу(RawUrl, "longitude").Replace(".", ",")) : 60.605514;
                            устройство.тип_сообщения = Из_строки_по_ключу(RawUrl, "message_type");
                            устройство.сообщение_по_умолчанию = Из_строки_по_ключу(RawUrl, "default_message");
                            устройство.сообщение_индивидуальное = Из_строки_по_ключу(RawUrl, "individual_message");
                            DateTime temp_time;
                            устройство.время_показа_индивидуального_сообщения = DateTime.TryParse(Из_строки_по_ключу(RawUrl, "individual_message_time"),out temp_time)?
                                DateTime.Parse(Из_строки_по_ключу(RawUrl, "individual_message_time")):DateTime.Now;
                            int temp_int;
                            устройство.время_показа = new TimeSpan(0, (int.TryParse(Из_строки_по_ключу(RawUrl, "marshrut_time"), out temp_int) ? int.Parse(Из_строки_по_ключу(RawUrl, "marshrut_time")) : 0), 0);
                            устройство.код_обновления_скрипта = int.TryParse(Из_строки_по_ключу(RawUrl, "script_code"), out temp_int) ? int.Parse(Из_строки_по_ключу(RawUrl, "script_code")) : 0;
                            устройство.комментарий = Из_строки_по_ключу(RawUrl, "comment");

                            string адрес_переадресации = Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь);

                            Отправка_ответа_на_запрос(response, Переадресация(5,"Изменения сохранены", request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/change_device?"))+ 
                                Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь)), RawUrl);

                            Запись_устройств_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} внес изменения в устройство с серийным номером " +устройство.серийный_номер);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} внес изменения в устройство с серийным номером " + устройство.серийный_номер, 3));
                            return;
                        }
                    case "/new_device":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            if (список_устройств.Count > 0)
                            {
                                response.AppendHeader("Set-Cookie", $"data={список_устройств[0].серийный_номер}; Max-age=5");
                            }

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Страница_нового_устройства.html"), RawUrl);
                            return;
                        }
                    case "/add_device":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string серийный_номер_из_запроса = Из_строки_по_ключу(RawUrl, "serial_number");
                            foreach (Устройство dev in список_устройств)
                            {
                                if (dev.серийный_номер == серийный_номер_из_запроса)
                                {
                                    Отправка_ответа_на_запрос(response, Переадресация(5, $"Новое устройство не создано! Устройство с серийным номером {серийный_номер_из_запроса} уже существует.",
                                        request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/add_device?")) + "/new_device"), RawUrl);
                                    return;
                                }
                            }

                            string имя_из_запроса = Из_строки_по_ключу(RawUrl, "name");
                            foreach (Устройство dev in список_устройств)
                            {
                                if (dev.имя == имя_из_запроса)
                                {
                                    Отправка_ответа_на_запрос(response, Переадресация(5, $"Новое устройство не создано! Устройство с именем {имя_из_запроса} уже существует.",
                                        request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/add_device?")) + "/new_device"), RawUrl);
                                    return;
                                }
                            }

                            Устройство устройство = new Устройство(серийный_номер_из_запроса);
                            устройство.имя = имя_из_запроса;
                            устройство.логин = Из_строки_по_ключу(RawUrl, "login");
                            устройство.пароль = Из_строки_по_ключу(RawUrl, "password");
                            устройство.телефон = Из_строки_по_ключу(RawUrl, "phone");
                            устройство.адрес = Из_строки_по_ключу(RawUrl, "address");
                            double temp;
                            устройство.широта = double.TryParse(Из_строки_по_ключу(RawUrl, "latitude").Replace(".", ","), out temp) ? double.Parse(Из_строки_по_ключу(RawUrl, "latitude").Replace(".", ",")) : 56.838607;
                            устройство.долгота = double.TryParse(Из_строки_по_ключу(RawUrl, "longitude").Replace(".", ","), out temp) ? double.Parse(Из_строки_по_ключу(RawUrl, "longitude").Replace(".", ",")) : 60.605514;
                            устройство.тип_сообщения = Из_строки_по_ключу(RawUrl, "message_type");
                            устройство.сообщение_по_умолчанию = Из_строки_по_ключу(RawUrl, "default_message");
                            устройство.сообщение_индивидуальное = Из_строки_по_ключу(RawUrl, "individual_message");
                            DateTime temp_time;
                            устройство.время_показа_индивидуального_сообщения = DateTime.TryParse(Из_строки_по_ключу(RawUrl, "individual_message_time"), out temp_time) ?
                                DateTime.Parse(Из_строки_по_ключу(RawUrl, "individual_message_time")) : DateTime.Now;
                            int temp_int;
                            устройство.время_показа = new TimeSpan(0, (int.TryParse(Из_строки_по_ключу(RawUrl, "marshrut_time"), out temp_int) ? int.Parse(Из_строки_по_ключу(RawUrl, "marshrut_time")) : 0), 0);
                            устройство.код_обновления_скрипта = int.TryParse(Из_строки_по_ключу(RawUrl, "script_code"), out temp_int) ? int.Parse(Из_строки_по_ключу(RawUrl, "script_code")) : 0;
                            устройство.комментарий = Из_строки_по_ключу(RawUrl, "comment");

                            список_устройств.Add(устройство);

                            Отправка_ответа_на_запрос(response, Переадресация(5, "Новое устройство создано", request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/add_device?")) +
                                Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь)), RawUrl);

                            Запись_устройств_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} создал новое устройство с серийным номером " + устройство.серийный_номер);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} создал новое устройство с серийным номером " + устройство.серийный_номер, 3));
                            return;
                        }
                    case "/del_device":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string серийный_номер_из_запроса = Из_строки_по_ключу(RawUrl, "serial_number");

                            Устройство устройство = null;
                            foreach (Устройство dev in список_устройств)
                            {
                                if (dev.серийный_номер == серийный_номер_из_запроса)
                                {
                                    устройство = dev;
                                    break;
                                }
                            }

                            if (устройство == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Устройство не удалено! Устройство с серийным номером {серийный_номер_из_запроса} не найдено.",
                                       request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/del_device?")) + "/devices"), RawUrl);
                                return;
                            }

                            список_устройств.Remove(устройство);

                            Запись_устройств_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} удалил устройство с серийным номером " + устройство.серийный_номер);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} удалил устройство с серийным номером " + устройство.серийный_номер, 3));

                            Отправка_ответа_на_запрос(response, Переадресация(5, $"Устройство с серийным номером {серийный_номер_из_запроса} было удалено.",
                                      request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/del_device?")) + "/devices"), RawUrl);
                            return;
                        }
                    case "/users":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Пользователи.html"), RawUrl);
                            return;
                        }
                    case "/get_users_datas":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;
                            
                            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                            {
                                Formatting = Newtonsoft.Json.Formatting.Indented,
                                DateFormatString = "dd.MM.yyyy HH:mm:ss"
                            };
                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");

                            string data = "";
                            if (логин_из_запроса == "")
                                data = JsonConvert.SerializeObject(список_пользователей, serializerSettings);
                            else
                            {
                                List<Пользователь> список = new List<Пользователь>();
                                foreach (Пользователь пользователь in список_пользователей)
                                {
                                    if (пользователь.логин == логин_из_запроса)
                                    {
                                        список.Add(пользователь);
                                        break;
                                    }
                                }
                                data = JsonConvert.SerializeObject(список, serializerSettings);
                            }
                            Отправка_ответа_на_запрос(response, data, RawUrl);
                            return;
                        }
                    case "/get_user_events":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;
                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");
                            List<Событие> список = new List<Событие>();
                            
                            foreach (Событие событие in список_событий)
                            {
                                if (событие.пользователь == логин_из_запроса)
                                {
                                    список.Add(событие);
                                }
                            }
                            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                            {
                                Formatting = Newtonsoft.Json.Formatting.Indented,
                                DateFormatString = "dd.MM.yyyy HH:mm:ss"
                            };
                            string data = JsonConvert.SerializeObject(список, serializerSettings);
                            Отправка_ответа_на_запрос(response, data, RawUrl);
                            return;
                        }
                    case "/user":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");

                            Пользователь пользователь = null;
                            foreach (Пользователь user in список_пользователей)
                            {
                                if (user.логин == логин_из_запроса)
                                {
                                    пользователь = user;
                                    break;
                                }
                            }

                            if (пользователь == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Внутренняя ошибка. Пользователь с логином {логин_из_запроса} не найден.",
                                     request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/user"))+"/users"), RawUrl);
                                return;
                            }
                            response.AppendHeader("Set-Cookie", $"data={пользователь.логин}; Max-age=5");
                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Пользователь.html"), RawUrl);
                            return;
                        }
                    case "/change_user":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");

                            Пользователь пользователь = null;
                            foreach (Пользователь user in список_пользователей)
                            {
                                if (user.логин == логин_из_запроса)
                                {
                                    пользователь = user;
                                    break;
                                }
                            }

                            if (пользователь == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Внутренняя ошибка. Пользователь с логином {логин_из_запроса} не найден.",
                                     request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/change_user"))+"/users"), RawUrl);
                                return;
                            }

                            пользователь.пароль = Из_строки_по_ключу(RawUrl, "password");
                            пользователь.класс = Из_строки_по_ключу(RawUrl, "class");

                            Запись_пользователей_на_диск();
                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} внес изменения в учетную запись пользователя " + пользователь.логин);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} внес изменения в учетную запись пользователя " + пользователь.логин, 4));
                            список_событий.Add(new Событие(пользователь.логин, $"Пользователь {сеанс.пользователь.логин} внес изменения в учетную запись пользователя " + пользователь.логин, 4));
                            Отправка_ответа_на_запрос(response, Переадресация(5, $"Изменения в учетной записи пользователя с логином {пользователь.логин} сохранены.",
                                     request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/change_user")) + "/users"), RawUrl);
                            return;
                        }
                    case "/del_user":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");

                            Пользователь пользователь = null;
                            foreach (Пользователь user in список_пользователей)
                            {
                                if (user.логин == логин_из_запроса)
                                {
                                    пользователь = user;
                                    break;
                                }
                            }

                            if (пользователь == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Пользователь не удален! Пользователь с логином {логин_из_запроса} не найден.",
                                       request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/del_user?")) + "/users"), RawUrl);
                                return;
                            }

                            список_пользователей.Remove(пользователь);

                            Запись_пользователей_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} удалил учетную запись пользователя с логином " + пользователь.логин);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} удалил учетную запись пользователя с логином " + пользователь.логин, 4));

                            Отправка_ответа_на_запрос(response, Переадресация(5, $"Пользователь с логином {логин_из_запроса} был удален.",
                                      request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/del_user?")) + "/users"), RawUrl);
                            return;
                        }
                    case "/new_user":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Новый_пользователь.html"), RawUrl);
                            return;
                        }
                    case "/add_user":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string логин_из_запроса = Из_строки_по_ключу(RawUrl, "login");
                            foreach (Пользователь user in список_пользователей)
                            {
                                if (user.логин == логин_из_запроса)
                                {
                                    Отправка_ответа_на_запрос(response, Переадресация(5, $"Новый пользователь не создан! Пользователь с логином {логин_из_запроса} уже существует.",
                                        request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/add_user?")) + "/new_user"), RawUrl);
                                    return;
                                }
                            }

                            Пользователь пользователь = new Пользователь();
                            пользователь.логин = логин_из_запроса;
                            пользователь.пароль = Из_строки_по_ключу(RawUrl, "password");
                            пользователь.класс = Из_строки_по_ключу(RawUrl, "class");

                            список_пользователей.Add(пользователь);

                            string адрес_переадресации = Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь);

                            Отправка_ответа_на_запрос(response, Переадресация(5, "Новый пользователь с логином "+пользователь.логин + " создан.", 
                                request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/add_user?")) + "/users"), RawUrl);

                            Запись_пользователей_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} создал нового пользователя с логином " + пользователь.логин);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} создал нового пользователя с логином " + пользователь.логин, 4));
                            список_событий.Add(new Событие(пользователь.логин, $"Пользователь {сеанс.пользователь.логин} создал нового пользователя с логином " + пользователь.логин, 4));
                            return;
                        }
                    case "/scripts":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Скрипты.html"), RawUrl);
                            return;
                        }
                    case "/get_scripts_datas":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;
                            string имя_скрипта_из_запроса = Из_строки_по_ключу(RawUrl, "name");

                            List<Скрипт> данные = new List<Скрипт>();

                            if (имя_скрипта_из_запроса == "")
                            {
                                данные = список_скриптов;
                            }
                            else
                            {
                                Скрипт скрипт = null;
                                foreach (Скрипт scr in список_скриптов)
                                {
                                    if (scr.имя == имя_скрипта_из_запроса)
                                    {
                                        скрипт = scr;
                                        break;
                                    }
                                }
                                if (скрипт != null)
                                {
                                    данные.Add(скрипт);
                                }
                            }

                            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                            {
                                Formatting = Newtonsoft.Json.Formatting.Indented,
                                DateFormatString = "dd.MM.yyyy HH:mm:ss"
                            };
                            string data = JsonConvert.SerializeObject(данные, serializerSettings);
                            Отправка_ответа_на_запрос(response, data, RawUrl);
                            return;
                        }
                    case "/script":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string имя_скрипта_из_запроса = Из_строки_по_ключу(RawUrl, "name");

                            Скрипт скрипт = null;
                            foreach (Скрипт scr in список_скриптов)
                            {
                                if (scr.имя==имя_скрипта_из_запроса)
                                {
                                    скрипт=scr;
                                    break;
                                }
                            }

                            if (скрипт == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Внутренняя ошибка. Скрипт с именем {имя_скрипта_из_запроса} не найден.",
                                     request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/script")) + "/scripts"), RawUrl);
                                return;
                            }
                            response.AppendHeader("Set-Cookie", $"data={список_скриптов.LastIndexOf(скрипт)}; Max-age=5");
                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Скрипт.html"), RawUrl);
                            return;
                        }
                    case "/get_script_name_from_number":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;
                            string номер_скрипта_из_запроса = Из_строки_по_ключу(RawUrl, "number");
                            
                            int temp;
                            int номер_скрипта = int.TryParse(номер_скрипта_из_запроса, out temp) ? int.Parse(номер_скрипта_из_запроса) : -1;
                            
                            if (номер_скрипта != -1 && номер_скрипта < список_скриптов.Count)
                            {
                                Отправка_ответа_на_запрос(response, список_скриптов[номер_скрипта].имя, RawUrl);
                                return;
                            }
                            else
                            {
                                Отправка_ответа_на_запрос(response, "", RawUrl);
                                return;
                            }
                        }
                    case "/get_script_code_from_number":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;
                            string номер_скрипта_из_запроса = Из_строки_по_ключу(RawUrl, "number");

                            int temp;
                            int номер_скрипта = int.TryParse(номер_скрипта_из_запроса, out temp) ? int.Parse(номер_скрипта_из_запроса) : -1;

                            if (номер_скрипта != -1 && номер_скрипта < список_скриптов.Count)
                            {
                                Отправка_ответа_на_запрос(response, список_скриптов[номер_скрипта].код, RawUrl);
                                return;
                            }
                            else
                            {
                                Отправка_ответа_на_запрос(response, "", RawUrl);
                                return;
                            }
                        }
                    case "/change_script":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string имя_скрипта_из_запроса = Из_строки_по_ключу(RawUrl, "name");

                            Скрипт скрипт = null;
                            foreach (Скрипт scr in список_скриптов)
                            {
                                if (scr.имя == имя_скрипта_из_запроса)
                                {
                                    скрипт = scr;
                                    break;
                                }
                            }

                            if (скрипт == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Внутренняя ошибка. Скрипт с именем {имя_скрипта_из_запроса} не найден.",
                                    request.Url.ToString().Remove(request.Url.ToString().LastIndexOf("/change_device?")) +
                                    Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь)), RawUrl);
                                return;
                            }

                            скрипт.код=Из_строки_по_ключу(RawUrl,"code");

                            Отправка_ответа_на_запрос(response, Переадресация(5, "Изменения сохранены", "/scripts"), RawUrl);

                            Запись_скриптов_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} внес изменения в скрипт с серийным именем " + скрипт.имя);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} внес изменения в скрипт с серийным именем " + скрипт.имя, 5));
                            return;
                        }
                    case "/new_script":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Новый_скрипт.html"), RawUrl);
                            return;
                        }
                    case "/add_script":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string имя_скрипта_из_запроса = Из_строки_по_ключу(RawUrl, "name");
                            foreach (Скрипт scr in список_скриптов)
                            {
                                if (scr.имя == имя_скрипта_из_запроса)
                                {
                                    Отправка_ответа_на_запрос(response, Переадресация(5, $"Новый скрипт не создан! Скрипт с именем {имя_скрипта_из_запроса} уже существует.","/new_script"), RawUrl);
                                    return;
                                }
                            }

                            Скрипт скрипт = new Скрипт();
                            скрипт.имя = имя_скрипта_из_запроса;
                            скрипт.код = Из_строки_по_ключу(RawUrl, "code");

                            список_скриптов.Add(скрипт);

                            string адрес_переадресации = Выбор_адреса_в_зависимости_от_класса_пользователя(сеанс.пользователь);

                            Отправка_ответа_на_запрос(response, Переадресация(5, "Новый скрипт с именем " + скрипт.имя + " создан.", "/scripts"), RawUrl);

                            Запись_скриптов_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} создал новый скрипт с именем " + скрипт.имя+".");
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} создал новый скрипт с именем " + скрипт.имя + ".", 5));
                            return;
                        }
                    case "/del_script":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            if (сеанс == null) return;
                            сеанс.время_последнего_запроса = DateTime.Now;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={сеанс.пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            string имя_скрипта_из_запроса = Из_строки_по_ключу(RawUrl, "name");

                            Скрипт скрипт = null;
                            foreach (Скрипт scr in список_скриптов)
                            {
                                if (scr.имя == имя_скрипта_из_запроса)
                                {
                                    скрипт = scr;
                                    break;
                                }
                            }

                            if (скрипт == null)
                            {
                                Отправка_ответа_на_запрос(response, Переадресация(5, $"Скрипт не удален! Скрипт с именем {имя_скрипта_из_запроса} не найден.","/scripts"), RawUrl);
                                return;
                            }

                            список_скриптов.Remove(скрипт);

                            Запись_скриптов_на_диск();

                            Console.WriteLine($"Пользователь {сеанс.пользователь.логин} удалил скрипт с именем " + скрипт.имя);
                            список_событий.Add(new Событие(сеанс.пользователь.логин, $"Пользователь {сеанс.пользователь.логин} удалил скрипт с именем " + скрипт.имя, 5));

                            Отправка_ответа_на_запрос(response, Переадресация(5, $"Скрипт с именем {имя_скрипта_из_запроса} был удален.","/scripts"), RawUrl);
                            return;
                        }
                    case "/messages_edds":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request))
                            {
                                Отправка_ответа_на_запрос(response, Загрузка_страницы("Авторизация.html"), RawUrl);
                                return;
                            }

                            Сеанс сеанс = Сеанс_из_куки(request);
                            сеанс.время_последнего_запроса = DateTime.Now;
                            Пользователь пользователь = сеанс.пользователь;

                            response.AddHeader("Set-Cookie", $"seans_key={сеанс.код_сеанса.ToString()}");
                            response.AppendHeader("Set-Cookie", $"user_login={пользователь.логин}");
                            response.AppendHeader("Set-Cookie", $"server_ip={request.Url.ToString().Remove(request.Url.ToString().IndexOf("16017") + 5)}");

                            Отправка_ответа_на_запрос(response, Загрузка_страницы("Сообщения_ЕДДС.html"), RawUrl);
                            return;
                        }
                    case "/get_messages_data":
                        {
                            if (!Проверка_логина_и_кода_сеанса_из_куки(request)) return;

                            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                            {
                                Formatting = Newtonsoft.Json.Formatting.Indented,
                                DateFormatString = "dd.MM.yyyy HH:mm:ss"
                            };
                            List<СообщениеЕДДС> список = new List<СообщениеЕДДС>();
                            foreach (СообщениеЕДДС сообщение in список_сообщенийЕДДС)
                            {
                                if (сообщение.время_начала < DateTime.Now && сообщение.время_конца > DateTime.Now)
                                {
                                    список.Add(сообщение);
                                }
                            }
                            string data = JsonConvert.SerializeObject(список, serializerSettings);

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

            static string Переадресация(int секунд, string сообщение, string Url)
            {
                XDocument xDoc = new XDocument();
                XElement html = new XElement("html");
                XElement head = new XElement("head",
                                new XElement("meta", new XAttribute("charset", "utf-8")),
                                new XElement("meta", new XAttribute("http-equiv", "refresh"),
                                    new XAttribute("content", $"{секунд};URL={Url}")),
                                new XElement("style",
                                    new XAttribute("type","text/css"),
                                    "\r\n.inner {\r\n" +
                                    "\tmargin: auto;\r\n" +
                                    "\tpadding:20px;\r\n" +
                                    "\tbackground-color: #cfc;" +
                                    "\tborder-radius: 20px;\r\n" +
                                    "\tbox-shadow: 0 0 10px rgba(0, 0, 0, 0.5);\r\n" +
                                    "\ttext-shadow: 0 1px 1px white;" +
                                    "\ttext-align: center;" +
                                    "\r\n}"));
                html.Add(head);
                if (сообщение != null)
                {
                    XElement body = new XElement("body",
                        new XElement("div",
                            new XAttribute("class","inner"),
                            new XAttribute("id","inner"),
                            new XElement("big",
                                new XElement("big", сообщение))));
                    html.Add(body);
                }
                xDoc.Add(html);
                return "<!DOCTYPE HTML>\r\n" + xDoc;
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

            static string Выбор_адреса_в_зависимости_от_класса_пользователя(Пользователь пользователь)
            {
                string адрес_переадресации = "";

                switch (пользователь.класс)
                {
                    case "Admin":
                        адрес_переадресации = "/main_devices_admin";
                        break;
                    case "COD":
                        адрес_переадресации = "/main_devices_cod";
                        break;
                    case "EDDS":
                        адрес_переадресации = "/messages_edds";
                        break;
                }
                return адрес_переадресации;
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

            static bool Проверка_логина_и_кода_сеанса_из_URL(string RawUrl)
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

            static bool Проверка_логина_и_кода_сеанса_из_куки(HttpListenerRequest request)
            {
                string логин = "";
                int номер_сеанса = 0;
                try
                {
                    логин = request.Cookies["user_login"].Value;
                    номер_сеанса = Int32.Parse(request.Cookies["seans_key"].Value);
                }
                catch(Exception e) { Console.WriteLine(e.Message); }

                bool сеанс_распознан = false;
                Сеанс сеанс = null;
                foreach (Сеанс seans in список_сеансов)
                {
                    if (seans.код_сеанса == номер_сеанса)
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

                if (сеанс.пользователь.логин != логин)
                {
                    Console.WriteLine($"Ошибка обработки запроса. Логин в запросе не совпадает с логином в активном сеансе. Обработка запроса прекращена.");
                    список_событий.Add(new Событие($"Ошибка обработки запроса. Логин в запросе не совпадает с логином в активном сеансе. Обработка запроса прекращена.", 31));
                    return false;
                }
                return true;
            }

            static Сеанс Сеанс_из_куки(HttpListenerRequest request)
            {
                Сеанс сеанс = null;
                int номер_сеанса = 0;
                try
                {
                    номер_сеанса = Int32.Parse(request.Cookies["seans_key"].Value);
                }
                catch { }

                foreach (Сеанс s in список_сеансов)
                {
                    if (s.код_сеанса == номер_сеанса)
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
            public bool онлайн { get; set; }

            public Пользователь()
            {
                логин = "";
                пароль = "";
                класс = "";
                онлайн = false;
            }

            public string ToJSON()
            {
                онлайн = false;
                foreach (Сеанс сеанс in список_сеансов)
                {
                    if (сеанс.пользователь.логин == логин)
                    {
                        онлайн = true;
                        break;
                    }
                }

                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
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
            //3-пользователь внес изменения в устройства
            //4-пользователь внес изменения в пользователя
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
            public string сообщение_по_умолчанию { get; set; }
            public string сообщение_индивидуальное { get; set; }
            public string комментарий { get; set; }
            public DateTime время_показа_индивидуального_сообщения { get; set; }
            public TimeSpan время_показа { get;set; }
            public List<DateTime> список_времени_выхода_на_связь { get; set; }
            public List<string> список_отображения_отправленных_скриптов { get; set; }
            public string имя_скрипта { get; set; }
            public int код_обновления_скрипта { get; set; }

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
                сообщение_по_умолчанию = "";
                сообщение_индивидуальное = "";
                комментарий = "";
                время_показа = new TimeSpan(0, 30, 0);
                время_показа_индивидуального_сообщения = DateTime.Now;
                список_времени_выхода_на_связь = new List<DateTime>();
                список_отображения_отправленных_скриптов = new List<string>();
                имя_скрипта = "";
                код_обновления_скрипта = 0;
            }
        }

        class ДанныеУстройстваДляВеб
        {
            public string serial_number { set; get; }
            public string login { get; set; }
            public string password { get; set; }
            public string name { set; get; }
            public string phone { set; get; }
            public string address { set; get; }
            public string type { set; get; }
            public string status { set; get; }
            public double latit { get; set; }
            public double longit { get; set; }
            public string default_message { get; set; }
            public string individual_message { set; get; }
            public string individual_message_time { set; get; }
            public string comment { get; set; }
            public List<DateTime> times { get; set; }
            public List<string> scripts { get; set; }
            public int visible_time { get; set; }
            public int update_code { get; set; }
            public List<string> list_scripts_names { get; set; }
            public string script_name { get; set; }

            public ДанныеУстройстваДляВеб(Устройство устройство)
            {
                this.serial_number = устройство.серийный_номер;
                this.login = устройство.логин;
                password = устройство.пароль;
                this.name = устройство.имя;
                phone = устройство.телефон;
                this.address = устройство.адрес;
                this.type = устройство.тип_сообщения;
                this.status = устройство.статус;
                this.latit = устройство.широта;
                this.longit = устройство.долгота;
                times = устройство.список_времени_выхода_на_связь;
                scripts = устройство.список_отображения_отправленных_скриптов;
                default_message = устройство.сообщение_по_умолчанию;
                individual_message = устройство.сообщение_индивидуальное;
                individual_message_time = устройство.время_показа_индивидуального_сообщения.Year+"-"+ 
                    (устройство.время_показа_индивидуального_сообщения.Month.ToString().Length==2? устройство.время_показа_индивидуального_сообщения.Month.ToString():"0"+ устройство.время_показа_индивидуального_сообщения.Month) +"-"+ 
                    (устройство.время_показа_индивидуального_сообщения.Day.ToString().Length == 2 ? устройство.время_показа_индивидуального_сообщения.Day.ToString() : "0" + устройство.время_показа_индивидуального_сообщения.Day) +"T"+
                    (устройство.время_показа_индивидуального_сообщения.Hour.ToString().Length == 2 ? устройство.время_показа_индивидуального_сообщения.Hour.ToString() : "0" + устройство.время_показа_индивидуального_сообщения.Hour) + ":"+
                    (устройство.время_показа_индивидуального_сообщения.Minute.ToString().Length == 2 ? устройство.время_показа_индивидуального_сообщения.Minute.ToString() : "0" + устройство.время_показа_индивидуального_сообщения.Minute);
                comment = устройство.комментарий;
                visible_time = устройство.время_показа.Minutes;
                update_code = устройство.код_обновления_скрипта;
                script_name = устройство.имя_скрипта;
                list_scripts_names = new List<string>();
                foreach (Скрипт скрипт in список_скриптов)
                {
                    list_scripts_names.Add(скрипт.имя);
                }
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
            public string имя;
            public string код;

            public bool Нужна_обработка()
            {
                if (код.IndexOf("@@@") != -1 || код.IndexOf("###") != -1)
                    return true;
                else
                    return false;
            }

            public string Обработка(Устройство устройство)
            {
                Данные_табло данные_табло = new Данные_табло(устройство);
                List<string> строки_сообщения = new List<string>();
                if (!данные_табло.данные_корректны)
                    return "Err";

                string цвет_температуры = "#22dd00";
                int n;
                if (Int32.TryParse(сервер.температура, out n))
                {
                    if (Convert.ToInt32(сервер.температура) <= -30)
                        цвет_температуры = "#0000ff";
                    if (Convert.ToInt32(сервер.температура) > -30 && Convert.ToInt32(сервер.температура) <= -15)
                        цвет_температуры = "#4444aa";
                    if (Convert.ToInt32(сервер.температура) > -15 && Convert.ToInt32(сервер.температура) <= 0)
                        цвет_температуры = "#ffffff";
                    if (Convert.ToInt32(сервер.температура) > 0 && Convert.ToInt32(сервер.температура) <= 15)
                        цвет_температуры = "#22dd00";
                    if (Convert.ToInt32(сервер.температура) > 15 && Convert.ToInt32(сервер.температура) <= 30)
                        цвет_температуры = "#aa5500";
                    if (Convert.ToInt32(сервер.температура) > 30)
                        цвет_температуры = "#ff0000";
                }

                List<Перестановка> Массив_перестановок = new List<Перестановка>()
                {
                    new Перестановка("\r\n","" ),
                    new Перестановка("\t",""),
                    new Перестановка("@@@temperatura",сервер.температура),
                    new Перестановка("@@@temp_color",цвет_температуры),
                };

                string текст_бегущей_строки = устройство.тип_сообщения == "По умолчанию" ? устройство.сообщение_по_умолчанию : устройство.тип_сообщения == "Индивидуальное" ? устройство.сообщение_индивидуальное : "";
                int количество_строк = 1;
                int размер_строки_по_вертикали = 12;
                string шрифт1 = "m";
                string шрифт2 = "m";
                string тип_скрипта = "0";

                string текст_скрипта = код;


                СообщениеЕДДС сообщение_экстренное = null;
                СообщениеЕДДС сообщение_информирование = null;

                for (; ; )
                {
                    if (текст_скрипта.IndexOf("###текст_бегущей_строки{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("###текст_бегущей_строки{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        текст_бегущей_строки = str;
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 2);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("###количество_строк{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("###количество_строк{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        количество_строк = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 2);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("###размер_строки_по_вертикали{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("###размер_строки_по_вертикали{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        размер_строки_по_вертикали = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 2);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("###шрифт1{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("###шрифт1{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        шрифт1 = str;
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 2);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("###шрифт2{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("###шрифт2{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        шрифт2 = str;
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 2);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("###тип_скрипта{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("###тип_скрипта{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        тип_скрипта = str;
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 2);
                        continue;
                    }

                    if (данные_табло.маршруты.Count == 0)
                        тип_скрипта = "нет информации";

                    break;
                }

                switch (тип_скрипта)
                {
                    case "стандарт":
                        {
                            if (данные_табло.маршруты.Count > 0)
                                for (int i = 0; i < данные_табло.маршруты.Count; i++)
                                {
                                    DateTime dateTime = DateTime.Parse(данные_табло.маршруты[i].tc_systime);
                                    DateTime dateTime2 = DateTime.Parse(данные_табло.маршруты[i].tc_arrivetime);
                                    if ((dateTime2 - dateTime) > устройство.время_показа)
                                    {
                                        for (int j = данные_табло.маршруты.Count - 1; j >= i; j--)
                                            данные_табло.маршруты.RemoveAt(j);
                                        break;
                                    }
                                }

                            if (список_сообщенийЕДДС.Count > 0)
                            {
                                foreach (СообщениеЕДДС сообщение in список_сообщенийЕДДС)
                                {
                                    if (сообщение.время_конца < DateTime.Now) continue;

                                    if ((DateTime.Now - сообщение.время_конца).Minutes % сообщение.период != 0) continue;

                                    foreach (string имя in сообщение.табло)
                                    {
                                        if (имя == устройство.имя)
                                        {
                                            сообщение.количество_показов++;
                                            if (сообщение.тип == "Экстренное")
                                                сообщение_экстренное = сообщение;
                                            if (сообщение.тип == "Информирование")
                                                сообщение_информирование = сообщение;
                                        }
                                    }
                                }
                            }

                            string цвет_бегущей_строки = "#00ff00";

                            if (сообщение_информирование != null)
                            {
                                текст_бегущей_строки = сообщение_информирование.текст;
                                цвет_бегущей_строки = сообщение_информирование.цвет;
                            }

                            текст_скрипта = "{\"st\": " +                                    //Массив объектов со сценами
                                               "[{\"sn\": " +                                   //Объект свойств сцены
                                               "{\"id\": 0, " +                                 //Идентификатор сцены, служит для возможностей организации переключений между сценами
                                               "\"bg\": 1, " +                                  //Признак того что сцена должна являться “фоновой”. При этом при переключении такие сцены остаются и отображаются вместе с новыми (0 - сцена не фоновая, 1 - сцена фоновая)
                                               "\"nx\": 1, " +                                  //Идентификатор сцены на которую следует переходить, после проигрывания текущей. Если никуда переходить не нужно, то не нужно указывать этот параметр, либо поставить -1
                                               "\"ws\": [{" +                                   //Массив свойств окон, из которых должна состоять сцена
                                                           "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                       "\"ef\": \"rtl\", " +    //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                       $"\"tx\": \"{текст_бегущей_строки.Replace("\"","''")}\"," +
                                                                       "\"av\": \"b\", " +      //Выравнивание текста внутри слоя по вертикали(0 или “top” или “t” -по верху; 1 или ”bottom” или "b" - по низу, 2 или "center" или "c" - по центру)
                                                                       "\"sp\": 15, " +         //Скорость бежания для бегущей
                                                                       $"\"cr\": \"{цвет_бегущей_строки}\", " +//Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                       $"\"ft\": \"{шрифт1}\"" +//Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                   "}]" +
                                                        "}]" +
                                               "}" +
                                             "}";

                            if (сообщение_экстренное != null)
                            {
                                string[] слова_сообщения = сообщение_экстренное.текст.Split(' ');
                                string строка_сообщения = "";
                                foreach (string слово in слова_сообщения)
                                {
                                    if ((строка_сообщения + слово).Length > 30)
                                    {
                                        строки_сообщения.Add(строка_сообщения);
                                        строка_сообщения = "";
                                    }
                                    строка_сообщения += слово;
                                }
                                строки_сообщения.Add(строка_сообщения);

                                for (int i = 0; i < строки_сообщения.Count / количество_строк; i++)
                                {
                                    int id;
                                    if (i < (double)строки_сообщения.Count / количество_строк - 1)
                                        id = i + 2;
                                    else
                                        id = 1;

                                    текст_скрипта += ", {\"sn\": {" +                                      //Объект свойств сцены
                                                        $"\"id\": {i + 1}, " +                             //Идентификатор сцены, служит для возможностей организации переключений между сценами
                                                         "\"pt\": 6, " +                                    //Количество секунд до переключения на следующую сцену. Если необходимо, чтоб сцена проигрывалась бесконечно нужно не указывать этот параметр (или поставить -1)
                                                        $"\"nx\": {id}, " +                              //Идентификатор сцены на которую следует переходить, после проигрывания текущей. Если никуда переходить не нужно, то не нужно указывать этот параметр, либо поставить -1
                                                         "\"ws\": [{" +                                    //Массив свойств окон, из которых должна состоять сцена
                                                                    "\"h\": 16, " +                       //Высота в пикселях  (действует для вложенных объектов - окна, слоя - если не переопределено)
                                                                    "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                                "\"x\": 1, " +
                                                                                "\"w\": 50, " +           //Ширина слоя в пикселях
                                                                                "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                                $"\"tx\": \"@@@temperatura°C\", " +
                                                                                "\"ah\": \"l\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                                $"\"cr\": \"@@@temp_color\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                                $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                             "}," +
                                                                            "{" +
                                                                                "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                                "\"tx\": \"@@@tc_systime_dd_mm_yyyy{0} / @@@tc_systime_hh_mm{0}\", " +
                                                                                "\"ah\": \"r\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                                "\"cr\": \"#ff3300\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                                $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                            "}]" +
                                                                "}, ";

                                    for (int j = 1; j <= количество_строк && j <= строки_сообщения.Count - i * количество_строк; j++)
                                    {
                                        текст_скрипта += "{" +
                                                            $"\"h\": {размер_строки_по_вертикали}, " +                        //Высота в пикселях  (действует для вложенных объектов - окна, слоя - если не переопределено)
                                                           $"\"y\": {размер_строки_по_вертикали * j + 2}, " +                   //координата Y относительно левого верхнего пикселя внутри окна
                                                            "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                        "\"x\": 1, " +            //координата X относительно левого верхнего пикселя внутри окна
                                                                        "\"w\": 192, " +           //Ширина слоя в пикселях
                                                                        "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                       $"\"tx\": \"{строки_сообщения[j - 1]}\", " +
                                                                        "\"ah\": \"c\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                       $"\"cr\": \"{сообщение_экстренное.цвет}\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                       $"\"ft\": \"{шрифт2}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                     "}]" +
                                                                    (j != количество_строк && j != данные_табло.маршруты.Count - i * количество_строк ? "}, " : "");
                                    }
                                    текст_скрипта += "}]}}";
                                }

                                текст_скрипта += "]}";
                            }
                            else
                            {
                                for (int i = 0; i < (double)данные_табло.маршруты.Count / количество_строк; i++)
                                {
                                    int id;
                                    if (i < (double)данные_табло.маршруты.Count / количество_строк - 1)
                                        id = i + 2;
                                    else
                                        id = 1;
                                    текст_скрипта += ", {\"sn\": {" +                                      //Объект свойств сцены
                                                        $"\"id\": {i + 1}, " +                             //Идентификатор сцены, служит для возможностей организации переключений между сценами
                                                         "\"pt\": 6, " +                                    //Количество секунд до переключения на следующую сцену. Если необходимо, чтоб сцена проигрывалась бесконечно нужно не указывать этот параметр (или поставить -1)
                                                        $"\"nx\": {id}, " +                              //Идентификатор сцены на которую следует переходить, после проигрывания текущей. Если никуда переходить не нужно, то не нужно указывать этот параметр, либо поставить -1
                                                         "\"ws\": [{" +                                    //Массив свойств окон, из которых должна состоять сцена
                                                                    "\"h\": 16, " +                       //Высота в пикселях  (действует для вложенных объектов - окна, слоя - если не переопределено)
                                                                    "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                                "\"x\": 1, " +
                                                                                "\"w\": 50, " +           //Ширина слоя в пикселях
                                                                                "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                                $"\"tx\": \"@@@temperatura°C\", " +
                                                                                "\"ah\": \"l\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                                $"\"cr\": \"@@@temp_color\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                                $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                             "}," +
                                                                            "{" +
                                                                                "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                                "\"tx\": \"@@@tc_systime_dd_mm_yyyy{0} / @@@tc_systime_hh_mm{0}\", " +
                                                                                "\"ah\": \"r\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                                "\"cr\": \"#ff3300\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                                $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                            "}]" +
                                                                "}, ";
                                    for (int j = 1; j <= количество_строк && j <= данные_табло.маршруты.Count - i * количество_строк; j++)
                                    {
                                        текст_скрипта += "{" +
                                                            $"\"h\": {размер_строки_по_вертикали}, " +                        //Высота в пикселях  (действует для вложенных объектов - окна, слоя - если не переопределено)
                                                           $"\"y\": {размер_строки_по_вертикали * j + 2}, " +                   //координата Y относительно левого верхнего пикселя внутри окна
                                                            "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                        "\"x\": 1, " +            //координата X относительно левого верхнего пикселя внутри окна
                                                                        "\"w\": 28, " +           //Ширина слоя в пикселях
                                                                        "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                        "\"tx\": \"@@@td_marshtitle{" + (i * количество_строк + j - 1) + "}\", " +
                                                                        "\"ah\": \"r\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                        "\"cr\": \"#ff3300\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                       $"\"ft\": \"{шрифт2}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                     "}, " +
                                                                     "{" +
                                                                        "\"x\": 36, " +           //координата X относительно левого верхнего пикселя внутри окна
                                                                        "\"w\": 116, " +          //Ширина слоя в пикселях
                                                                        "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                        "\"tx\": \"@@@td_dirtitle{" + (i * количество_строк + j - 1) + "}\", " +
                                                                        "\"ah\": \"l\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                        "\"cr\": \"#ff3300\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                       $"\"ft\": \"{шрифт2}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                     "}, " +
                                                                     "{" +
                                                                        "\"x\": 151, " +          //координата X относительно левого верхнего пикселя внутри окна
                                                                        "\"w\": 152, " +          //Ширина слоя в пикселях
                                                                        "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                        "\"tx\": \"@@@tc_arrivetime_m{" + (i * количество_строк + j - 1) + "}\", " +
                                                                        "\"ah\": \"r\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                        "\"cr\": \"#ff3300\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                       $"\"ft\": \"{шрифт2}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                    "}]" +
                                                                    (j != количество_строк && j != данные_табло.маршруты.Count - i * количество_строк ? "}, " : "");
                                    }
                                    текст_скрипта += "}]}}";
                                }
                                текст_скрипта += "]}";
                            }

                            break;
                        }
                    case "нет информации":
                        {
                            текст_скрипта = "{\"st\": " +                                    //Массив объектов со сценами
                                            "[{\"sn\": " +                                   //Объект свойств сцены
                                            "{\"id\": 0, " +                                 //Идентификатор сцены, служит для возможностей организации переключений между сценами
                                            "\"bg\": 1, " +                                  //Признак того что сцена должна являться “фоновой”. При этом при переключении такие сцены остаются и отображаются вместе с новыми (0 - сцена не фоновая, 1 - сцена фоновая)
                                            "\"nx\": 1, " +                                  //Идентификатор сцены на которую следует переходить, после проигрывания текущей. Если никуда переходить не нужно, то не нужно указывать этот параметр, либо поставить -1
                                            "\"ws\": [{" +                                   //Массив свойств окон, из которых должна состоять сцена
                                                        "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                    "\"ef\": \"rtl\", " +    //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                    $"\"tx\": \"{текст_бегущей_строки}\"," +
                                                                    "\"av\": \"b\", " +      //Выравнивание текста внутри слоя по вертикали(0 или “top” или “t” -по верху; 1 или ”bottom” или "b" - по низу, 2 или "center" или "c" - по центру)
                                                                    "\"sp\": 15, " +         //Скорость бежания для бегущей
                                                                    "\"cr\": \"#00ff00\", " +//Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                    $"\"ft\": \"{шрифт1}\"" +//Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                "}]" +
                                                     "}]" +
                                            "}" +
                                          "}";

                            текст_скрипта += ", {\"sn\": {" +                                      //Объект свойств сцены
                                                    $"\"id\": 1, " +                             //Идентификатор сцены, служит для возможностей организации переключений между сценами
                                                     "\"pt\": 6, " +                                    //Количество секунд до переключения на следующую сцену. Если необходимо, чтоб сцена проигрывалась бесконечно нужно не указывать этот параметр (или поставить -1)
                                                    $"\"nx\": 1, " +                              //Идентификатор сцены на которую следует переходить, после проигрывания текущей. Если никуда переходить не нужно, то не нужно указывать этот параметр, либо поставить -1
                                                     "\"ws\": [{" +                                    //Массив свойств окон, из которых должна состоять сцена
                                                                "\"h\": 16, " +                       //Высота в пикселях  (действует для вложенных объектов - окна, слоя - если не переопределено)
                                                                "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                            "\"x\": 1, " +
                                                                            "\"w\": 50, " +           //Ширина слоя в пикселях
                                                                            "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                            $"\"tx\": \"@@@temperatura°C\", " +
                                                                            "\"ah\": \"l\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                            $"\"cr\": \"@@@temp_color\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                            $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                         "}," +
                                                                        "{" +
                                                                            "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                            $"\"tx\": \"{DateTime.Now.ToString("dd.MM.yyyy")} / {(DateTime.Now + new TimeSpan(2, 0, 0)).ToString("HH:mm")}\", " +
                                                                            "\"ah\": \"r\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                            "\"cr\": \"#ff3300\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                            $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                        "}]" +
                                                            "}, ";
                            текст_скрипта += "{\"h\":16,\"y\":24,\"ls\":[{\"x\":1,\"w\":200,\"ef\":\"s\",\"tx\":\"Информация отсутствует\",\"ah\":\"c\",\"cr\":\"#cc0000\",\"ft\":\"m\"}]}]}}]}";
                            break;
                        }
                    case "сообщение":
                        {
                            string строки = "";
                            for (int i = 0; i < текст_бегущей_строки.Length; i += 30)
                            {
                                строки += "{\"h\":16,\"y\":" + $"{размер_строки_по_вертикали * (i + 1)}" + ",\"ls\":[{\"x\":1,\"w\":200,\"ef\":\"s\",\"tx\":" + $"\"{(текст_бегущей_строки.Substring(i).Length > 30 ? текст_бегущей_строки.Substring(i, 30) : текст_бегущей_строки.Substring(i))}, \",\"ah\":\"c\",\"cr\":\"#cc0000\",\"ft\":\"{шрифт1}\"" + "}]},";
                            }
                            строки = строки.Remove(строки.LastIndexOf(","));

                            текст_скрипта += "{\"st\":[{\"sn\":{\"id\":0,\"ws\":[{" +                                    //Массив свойств окон, из которых должна состоять сцена
                                                                "\"h\": 16, " +                       //Высота в пикселях  (действует для вложенных объектов - окна, слоя - если не переопределено)
                                                                "\"ls\": [{" +                       //Массив свойств слоев, которые содержатся внутри окна
                                                                            "\"x\": 1, " +
                                                                            "\"w\": 50, " +           //Ширина слоя в пикселях
                                                                            "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                            $"\"tx\": \"@@@temperatura°C\", " +
                                                                            "\"ah\": \"l\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                            $"\"cr\": \"@@@temp_color\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                            $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                         "}," +
                                                                        "{" +
                                                                            "\"ef\": \"s\", " +       //Тип эффекта, с которым воспроизводится текст (0 или “static” или “s" - статический текст; 1 или “right_to_left” или «rtl» - бегущая строка справа налево)
                                                                            $"\"tx\": \"{DateTime.Now.ToString("dd.MM.yyyy")} / {(DateTime.Now + new TimeSpan(2, 0, 0)).ToString("HH:mm")}\", " +
                                                                            "\"ah\": \"r\", " +       //Выравнивание текста внутри слоя по горизонтали (0 или “left" или "l" - по левому краю; 1 или "right" или "r" - по правому краю, 2  или "center" или "c" - по центру)
                                                                            "\"cr\": \"#ff3300\", " + //Цвет текста в слое("blue" или "b" - синий; "red" или "r" - красный; "green" или "g" - зеленый; "yellow" или "y" - желтый) цвет также может быть задан в виде hex значения для трех байт вида #RRGGBB (например #ffffff - белый)
                                                                            $"\"ft\": \"{шрифт1}\"" +        //Размер шрифта  (0  или “small” или “s” - малый; 1 или "medium@ или "m" - средний,; 2 или "large" или "l" - большой)
                                                                        "}]}," + $"{строки}" + "]}]}";
                            break;
                        }

                }

                for (int i = 0; i < Массив_перестановок.Count; i++)
                    текст_скрипта = текст_скрипта.Replace(Массив_перестановок[i].a, Массив_перестановок[i].b);

                for (; ; )
                {
                    if (текст_скрипта.IndexOf("@@@td_id{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@td_id{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].td_id);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@tb_id{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@tb_id{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].tb_id);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@td_marshtitle{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@td_marshtitle{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].td_marshtitle);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@td_dirtitle{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@td_dirtitle{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].td_dirtitle);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@td_template{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@td_template{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].td_template);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@tc_systime{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@tc_systime{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].tc_systime);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@tc_arrivetime{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@tc_arrivetime{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].tc_arrivetime);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@u_inv{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@u_inv{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].u_inv);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@td_marshtitle_en{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@td_marshtitle_en{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].td_marshtitle_en);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@tc_systime_dd_mm_yyyy{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@tc_systime_dd_mm_yyyy{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        DateTime dateTime = DateTime.Parse(данные_табло.маршруты[номер_маршрута].tc_systime);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, dateTime.ToString("dd.MM.yyyy"));
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@tc_systime_hh_mm{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@tc_systime_hh_mm{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        DateTime dateTime = DateTime.Parse(данные_табло.маршруты[номер_маршрута].tc_systime);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, dateTime.ToString("HH:mm"));
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@td_dirtitle_en{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@td_dirtitle_en{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, данные_табло.маршруты[номер_маршрута].td_dirtitle_en);
                        continue;
                    }

                    if (текст_скрипта.IndexOf("@@@tc_arrivetime_m{") != -1)
                    {
                        int t_f = текст_скрипта.IndexOf("@@@tc_arrivetime_m{");
                        int t_s = текст_скрипта.Substring(t_f).IndexOf("}");
                        string str = текст_скрипта.Substring(t_f);
                        str = str.Remove(t_s);
                        str = str.Substring(str.IndexOf("{") + 1);
                        int номер_маршрута = Convert.ToInt32(str);
                        DateTime dateTime = DateTime.Parse(данные_табло.маршруты[номер_маршрута].tc_systime);
                        DateTime dateTime2 = DateTime.Parse(данные_табло.маршруты[номер_маршрута].tc_arrivetime);
                        текст_скрипта = текст_скрипта.Remove(t_f, t_s + 1).Insert(t_f, (dateTime2 - dateTime).Hours > 0 ?
                            (dateTime2 - dateTime).Hours + " час" : (dateTime2 - dateTime).Minutes + " мин");
                        continue;
                    }
                    break;
                }

                устройство.список_времени_выхода_на_связь.Insert(0, DateTime.Now);
                if (устройство.список_времени_выхода_на_связь.Count > 30)
                    устройство.список_времени_выхода_на_связь.RemoveAt(30);

                string отображение_скрипта = сервер.температура + "°C\t\t";

                DateTime dateTime1 = DateTime.Parse(данные_табло.маршруты[0].tc_systime);
                отображение_скрипта += dateTime1.ToString("dd.MM.yyyy") + " / " + dateTime1.ToString("HH:mm") + "\r\n";

                if (сообщение_экстренное == null)
                {
                    if (данные_табло.маршруты.Count > 0)
                    {
                        for (int i = 0; i < данные_табло.маршруты.Count; i++)
                        {
                            if (i % количество_строк == 0)
                                отображение_скрипта += "<br>----------------------------------------<br>";
                            string temp = данные_табло.маршруты[i].td_dirtitle.Length > 25 ? данные_табло.маршруты[i].td_dirtitle.Remove(25) : данные_табло.маршруты[i].td_dirtitle;
                            if (temp.Length < 25)
                                while (temp.Length < 25) temp += " ";
                            отображение_скрипта += $"{данные_табло.маршруты[i].td_marshtitle} {temp} ";
                            DateTime dateTime = DateTime.Parse(данные_табло.маршруты[i].tc_systime);
                            DateTime dateTime2 = DateTime.Parse(данные_табло.маршруты[i].tc_arrivetime);
                            temp = ((dateTime2 - dateTime).Hours > 0 ? (dateTime2 - dateTime).Hours + " час" : (dateTime2 - dateTime).Minutes + " мин");
                            if (temp.Length < 6) temp = " " + temp;
                            отображение_скрипта += temp + "<br>";
                        }
                    }
                    else
                    {
                        отображение_скрипта += "<br>Данные о маршрутах отсутствуют<br>";
                    }
                }
                else
                {
                    for (int i = 0; i < строки_сообщения.Count; i++)
                    {
                        if (i % количество_строк == 0)
                            отображение_скрипта += "<br>----------------------------------------<br>";
                        отображение_скрипта += строки_сообщения[i] + "<br>";
                    }
                }
                отображение_скрипта += "----------------------------------------<br>";

                for (int i = 0; i < текст_бегущей_строки.Length; i += 36)
                {
                    текст_бегущей_строки = текст_бегущей_строки.Insert(i, "<br>");
                }
                отображение_скрипта += текст_бегущей_строки;
                устройство.список_отображения_отправленных_скриптов.Insert(0, отображение_скрипта);

                if (устройство.список_отображения_отправленных_скриптов.Count > 30)
                    устройство.список_отображения_отправленных_скриптов.RemoveAt(30);

                return текст_скрипта;
            }


            class Перестановка
            {
                public string a { get; set; }
                public string b { get; set; }
                public Перестановка(string a, string b)
                {
                    this.a = a;
                    this.b = b;
                }
            }

            class Данные_табло
            {
                public List<Маршрут> маршруты;
                public bool данные_корректны;

                public Данные_табло(Устройство устройство)
                {
                    маршруты = new List<Маршрут>();

                    try
                    {
                        XmlDocument xDoc = new XmlDocument();
                        string Text;
                        string site = сервер.настройки.адрес_сервера_предсказаний + "/getTabloData.php?_user_=" + устройство.логин
                            + "&_password_=" + устройство.пароль;

                        HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(site);
                        HttpWebResponse resp = (HttpWebResponse)req.GetResponse();

                        using (StreamReader stream = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            Text = stream.ReadToEnd();
                        }

                        xDoc.LoadXml(Text);
                        resp.Close();

                        XmlElement xRoot = xDoc.DocumentElement;
                        foreach (XmlNode row in xRoot)
                        {
                            Маршрут маршрут = new Маршрут();

                            XmlNode attr = row.Attributes.GetNamedItem("td_id");
                            маршрут.td_id = attr.Value;

                            attr = row.Attributes.GetNamedItem("tb_id");
                            маршрут.tb_id = attr.Value;

                            attr = row.Attributes.GetNamedItem("td_marshtitle");
                            маршрут.td_marshtitle = attr.Value;

                            attr = row.Attributes.GetNamedItem("td_dirtitle");
                            маршрут.td_dirtitle = attr.Value;

                            attr = row.Attributes.GetNamedItem("td_template");
                            маршрут.td_template = attr.Value;

                            attr = row.Attributes.GetNamedItem("tc_systime");
                            маршрут.tc_systime = attr.Value;

                            attr = row.Attributes.GetNamedItem("tc_arrivetime");
                            маршрут.tc_arrivetime = attr.Value;

                            attr = row.Attributes.GetNamedItem("u_inv");
                            маршрут.u_inv = attr.Value;

                            attr = row.Attributes.GetNamedItem("td_marshtitle_en");
                            маршрут.td_marshtitle_en = attr.Value;

                            attr = row.Attributes.GetNamedItem("td_dirtitle_en");
                            маршрут.td_dirtitle_en = attr.Value;

                            маршруты.Add(маршрут);
                        }

                        if (маршруты.Count > 1)
                            for (int i = 1; i < маршруты.Count; i++)
                                if (DateTime.Parse(маршруты[i].tc_arrivetime) < DateTime.Parse(маршруты[i - 1].tc_arrivetime))
                                {
                                    Маршрут маршрут = маршруты[i - 1];
                                    маршруты[i - 1] = маршруты[i];
                                    маршруты[i] = маршрут;
                                    i = 0;
                                }

                        данные_корректны = true;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Ошибка полуучения данных табло." +e.Message);
                        список_событий.Add(new Событие($"Ошибка полуучения данных табло." + e.Message, 23));
                        данные_корректны = false;
                    }
                }
            }

            public class Маршрут
            {
                public string td_id;
                public string tb_id;
                public string td_marshtitle;
                public string td_dirtitle;
                public string td_template;
                public string tc_systime;
                public string tc_arrivetime;
                public string u_inv;
                public string td_marshtitle_en;
                public string td_dirtitle_en;
            }
        }

        public class СообщениеЕДДС
        {
            public int ID { get; set; }
            public DateTime время_начала { get; set; }
            public DateTime время_конца { get; set; }
            public string тип { get; set; }
            public string текст { get; set; }
            public string цвет { get; set; }
            public int период { get; set; }
            public List<string> табло { get; set; }
            public int количество_показов { get; set; }

            public СообщениеЕДДС(int id, DateTime начало, DateTime конец, string тип_сообщения)
            {
                ID = id;
                время_начала = начало;
                время_конца = конец;
                тип = тип_сообщения;
                текст = "";
                цвет = "#ff0000";
                период = 1;
                табло = new List<string>();
                количество_показов = 0;
            }
        }

    }
}
