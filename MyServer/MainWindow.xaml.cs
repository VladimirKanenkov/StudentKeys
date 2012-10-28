using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MyServer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Настройки подключения, размер буфера
        const int port = 11000;
        const int socketSize = 100;
        const int bufferSize = 1024;
        #endregion
        string baseDir;//дирректория, в которой будут храниться принятые данные
        static IPEndPoint localEndPoint;//наша итоговая точка подключения
        static Socket listener;//серверный сокет для прослушки входящих соединений
        static List<StateObject> clients = new List<StateObject>();//списко из наших подключенных клиентов
        #region ManualResetEvent
        //служат для управления процессом выполнения приложения - ожидание пока какая либо задача выполнится
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private static ManualResetEvent infoDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        #endregion

        #region Вспомогательные функции
        /// <summary>
        /// Создает пустую папку где будем хранить временные результаты, предварительно очищая предыдущую
        /// </summary>
        private void CreateEmptyDir()
        {
            baseDir = Directory.GetCurrentDirectory()+"\\TestPoligon\\";
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
            Directory.CreateDirectory(baseDir);
            browseButton.IsEnabled = true;
        }
        /// <summary>
        /// Выводит результаты в нашу псевдо-консоль при помощи Dispatcher
        /// </summary>
        /// <param name="info">Строка, которую необходимо отобразить</param>
        void WriteStatus(string info)
        {
            //используем диспатчер, т.к. находимся не в потоке GUI
            this.Dispatcher.BeginInvoke
                (DispatcherPriority.Normal,
                    (ThreadStart)delegate()
                    {
                        ConsoleTextBox.Text += info + "\r\n";
                    }
            );
        }
        /// <summary>
        /// Десериализуем наши принятые данные
        /// </summary>
        /// <param name="pathToFile">Путь к файлу</param>
        /// <returns>Объект, представляющий наш файл + информацию о пользователе</returns>
        public MyClient.MyPacketWrapper DeserializeData(string pathToFile)
        {
            byte[] buffer = File.ReadAllBytes(pathToFile);
            //можно делать проверку на количество доступной памяти
            MemoryStream ms = new MemoryStream();
            // Записываем наши данные(buffer) в поток памяти
            ms.Write(buffer, 0, buffer.Length);

            BinaryFormatter serializer = new BinaryFormatter();
            //получаем наш объект
            ms.Position = 0;//!! обязательно, чтобы корректно считать с начала
            MyClient.MyPacketWrapper myPacket = (MyClient.MyPacketWrapper)serializer.Deserialize(ms);

            ms.Close();

            WriteStatus("Deserialization completed, size receive byte: " + buffer.Length);

            return myPacket;
        }
        /// <summary>
        /// Сохраняем принятый бинарник
        /// </summary>
        /// <param name="myPacket">Объект, представляющий пакет с данными</param>
        /// <param name="clientNum">Номер клиента</param>
        public void SaveData(MyClient.MyPacketWrapper myPacket, int clientNum)
        {
            try
            {
                string path = baseDir + "Client" + clientNum;
                if (!Directory.Exists(path))
                    //создаем дирректорию
                    Directory.CreateDirectory(path);

                string pathToFile = path + @"\" + myPacket.FileName;
                File.WriteAllBytes(pathToFile, myPacket.FileBuff);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }

        }
        /// <summary>
        /// Отображает данные о пользователе в псевдоконсоль
        /// </summary>
        /// <param name="myPacket"></param>
        public void ShowData(MyClient.MyPacketWrapper myPacket)
        {
            this.Dispatcher.BeginInvoke
                (DispatcherPriority.Normal,
                    (ThreadStart)delegate()
                    {
                        ConsoleTextBox.Text += "ФИО: " + myPacket.UserDetails.FullName + "\r\n";
                        ConsoleTextBox.Text += "Название учебного заведения: " + myPacket.UserDetails.University + "\r\n";
                        ConsoleTextBox.Text += "Номер телефона: " + myPacket.UserDetails.Phone + "\r\n";
                    }
            );
        }
        #endregion

        #region Запуск сервера, начало прослушки всех входящих соеднинений
        /// <summary>
        /// Создаем наш сокет, получаем/определяем ип сервера.
        /// </summary>
        private bool ServerPrepare()
        {
            try
            {
                localEndPoint = new IPEndPoint(IPAddress.Any, 11000);

                // Создаем наш сокет
                listener = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);

                WriteStatus("Socket was created");

                return true;
            }
            catch (SocketException exc)
            {
                MessageBox.Show(exc.Message);
                return false;
            }
        }
        /// <summary>
        /// Привязываем сокет к IPEndPoint, указываем кол-во одновременных подклчений и начинаем слушать
        /// </summary>
        public void StartListening()
        {
            if (ServerPrepare())
            {

                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(socketSize);

                    WriteStatus("Waiting for a connection...");

                    AcceptConncetions();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
            }

        }
        public void AcceptConncetions()
        {
            while (true)
            {
                // Set the event to nonsignaled state.
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                // Wait until a connection is made before continuing.
                allDone.WaitOne();
            }
        }
        #endregion

        #region Получаем соединения и файлы
        /// <summary>
        /// Получает подключение, заносит его в объект состояния и общий список всех подключений
        /// </summary>
        /// <param name="ar"></param>
        public void AcceptCallback(IAsyncResult ar)
        {
            // Сигнализируем основному потоку продолжить
            allDone.Set();
            // Получаем сокет, который инициировал подключение
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);//завершаем старый поток(получения сокета клиента)

            // Создаем наш объект состояния подключения
            StateObject state = new StateObject();
            state.workSocket = handler;
            //присваиваем каждому свой номер
            state.clientNum = StateObject.countClient;
            //Считаем кол-во клиентов
            StateObject.countClient++;

            //добавим в общий списко клиентов
            clients.Add(state);

            //добавляем в comboBox
            this.Dispatcher.BeginInvoke
                (DispatcherPriority.Normal,
                    (ThreadStart)delegate()
                    {
                        clientComboBox.Items.Add(state.clientNum);
                    }
            );

            WriteStatus("Client " + state.clientNum + " was connected");

            infoDone.Reset();

            //принимаем данные о размере пакета с данными или сообщение о разрыве связи
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                 new AsyncCallback(ReadInfoCallback), state);
            infoDone.WaitOne();

            //если передались данные о размере и присутсвует соединение - можем принимать основные данные
            if (handler.Connected)
            {
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
            }
            else
            {
                // удаляем из списка и из комбо бокса
                clients.Remove(state);
                this.Dispatcher.BeginInvoke
                    (DispatcherPriority.Normal,
                        (ThreadStart)delegate()
                        {
                            clientComboBox.Items.Remove(state.clientNum);
                        }
                    );

                //выводим сообщение об отключении клиента
                WriteStatus("Client " + state.clientNum + " was disconnected\r\n");
            }
        }
        /// <summary>
        /// Получаем размер файла
        /// </summary>
        /// <param name="ar">Хранит в себе результат</param>
        public void ReadInfoCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try
            {
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    string message = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                    if (message != "DISCONNECT")
                    {
                        WriteStatus("size=" + message);

                        try
                        {
                            state.sizePacket = long.Parse(message);
                        }
                        catch (Exception exc)
                        {
                            MessageBox.Show(exc.Message);
                        }
                    }
                    else
                    {
                        state.sizePacket = -1;
                        //завершаем подключение
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Пропала связь при приеме размера данных. Error: " + exc.Message);
                //handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            finally
            {
                infoDone.Set();
            }
        }
        /// <summary>
        /// Получает данные, записывает их в бинарный файл
        /// </summary>
        /// <param name="ar">Представляет состояние асинхронной операции</param>
        public void ReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try//смотрим не пропала ли связть(с помощью handler.EndReceive - SocketException
            {
                int bytesRead = handler.EndReceive(ar);

                state.sizeReceived += bytesRead;
                if (bytesRead > 0)
                {
                    string currentPath = baseDir + "Data" + state.clientNum + ".bin";

                    BinaryWriter writer = new BinaryWriter(File.Open(currentPath, FileMode.Append));

                    writer.Write(state.buffer, 0, bytesRead);
                    writer.Close();

                    //Продолжаем принимать пока не совпадут размеры
                    if (state.sizePacket != state.sizeReceived)
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            new AsyncCallback(ReadCallback), state);
                    }
                    else
                    {
                        WriteStatus("File was received");

                        MyClient.MyPacketWrapper myPacket = DeserializeData(currentPath);
                        SaveData(myPacket, state.clientNum);
                        ShowData(myPacket);
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Пропала связь при приеме данных. Error: " + exc.Message);
            }
        }
        #endregion

        #region Отправляем данные нашему клиенту
        /// <summary>
        /// Приводит строковые данные к байтовому виду и отправляет по сети
        /// </summary>
        /// <param name="handler">Текущий сокет для отправки</param>
        /// <param name="data">Данные, которые надо отправить</param>
        private void Send(Socket handler, String data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                int bytesSent = handler.EndSend(ar);

                WriteStatus("Sent " + bytesSent + " bytes to client");

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
                WriteStatus("Client was disconnected\r\n");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            finally
            {
                sendDone.Set();
            }
        }
        #endregion

        #region Кнопки
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (clientComboBox.Items.Count != 0)
            {
                //определяем номер выбранного клиента
                var selectItem = clientComboBox.SelectedItem;
                if (selectItem != null)
                {
                    int cbNum = (int)selectItem;

                    //ищем в списке подлючений, выбираем нужный
                    StateObject currentClient = clients.First((item) => item.clientNum == cbNum);
                    //Если данные были корректно приняты
                    if (currentClient.sizePacket == currentClient.sizeReceived && currentClient.sizeReceived > 0)
                    {
                        //считываем данные с полей ввода
                        string login = LoginBox.Text;
                        string password = PasswordBox.Text;

                        if (login != string.Empty && password != string.Empty)
                        {
                            //Отправляемая строка данных
                            string message = "Login: " + login + "\r\n" + "Password: " + password + "\r\n";

                            //отправляем данные,закрывает подлючение
                            Send(currentClient.workSocket, message);
                            //ждем пока не завершится передача
                            sendDone.WaitOne();

                            // удаляем из списка и из комбо бокса по завершению передачи данных
                            clients.Remove(currentClient);
                            clientComboBox.Items.Remove(clientComboBox.SelectedItem);
                        }
                        else
                        {
                            MessageBox.Show("Введите Login и Password");
                        }
                    }
                    else //иначе ждем когда придут.Уведомляем пользователя
                    {
                        MessageBox.Show("Сначала необходимо получить и проверить данные от пользователя(фотографию, документ)");
                    }
                }
                else
                {
                    MessageBox.Show("Выберите элемент");
                }
            }
            else
            {
                MessageBox.Show("Нет доступных подключений");
            }
        }
        private void browseButton_Click(object sender, RoutedEventArgs e)
        {
            //Открывает проводник, в заданной дирректории
            Process.Start("explorer.exe", baseDir);
        }
        #endregion
        public MainWindow()
        {
            InitializeComponent();
            //Cервер запускается каждый раз с пустой папкой данных.
            CreateEmptyDir();

            //Основная логика начинается в новой задаче (потоки)
            Task listen = Task.Factory.StartNew(StartListening);
        }
        
    }
}
