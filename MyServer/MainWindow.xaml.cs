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
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MyServer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region SettingsConst
        // The port number for the remote device.
        const int port = 11000;
        const int socketSize = 100;
        const int bufferSize = 1024;
        #endregion
        string baseDir;
        static IPEndPoint localEndPoint;
        static Socket listener;//серверный сокет для прослушки входящих соединений
        static List<StateObject> clients = new List<StateObject>();
        #region Thread signal
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
        /// Выводит результаты в нашу псевдо-консоль
        /// </summary>
        /// <param name="info">Строка, которую необходимо отобразить</param>
        void WriteStatus(string info)
        {
            this.Dispatcher.BeginInvoke
                (DispatcherPriority.Normal,
                    (ThreadStart)delegate()
                    {
                        ConsoleTextBox.Text += info + "\r\n";
                    }
            );
        }
        public MyClient.MyPacketWrapper DeserializeData(string pathToFile)
        {
            byte[] buffer = File.ReadAllBytes(pathToFile);
            //можно делать проверку на количество доступной памяти
            MemoryStream ms = new MemoryStream();
            // Записываем наши данные(buffer) в поток памяти
            ms.Write(buffer, 0, buffer.Length);

            BinaryFormatter serializer = new BinaryFormatter();
            //получаем наш объект
            ms.Position = 0;
            MyClient.MyPacketWrapper myPacket = (MyClient.MyPacketWrapper)serializer.Deserialize(ms);

            ms.Close();

            WriteStatus("Deserialization completed, size receive byte: " + buffer.Length);
            // return myPacket;
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
            catch (Exception)
            {

                throw;
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

                // Create a TCP/IP socket.
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
        public void StartListening()
        {
            if (ServerPrepare())
            {
                // Bind the socket to the local endpoint and listen for incoming connections.
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
        #region Получаем файлы, сохраняем, разбираем, отображаем результат
        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            allDone.Set();
            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);//завершаем старый поток(получения сокета клиента)

            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = handler;
            //присваиваем каждому свой номер
            state.clientNum = StateObject.countClient;
            //Считаем кол-во клиентов
            StateObject.countClient++;

            //добавим в общий списко клиентов
            clients.Add(state);

            this.Dispatcher.BeginInvoke
                (DispatcherPriority.Normal,
                    (ThreadStart)delegate()
                    {
                        clientComboBox.Items.Add(state.clientNum);
                    }
            );
            

            WriteStatus("Client " + state.clientNum + " was connected");

            infoDone.Reset();

            //принимаем данные о размере пакета с данными
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                 new AsyncCallback(ReadInfoCallback), state);
            infoDone.WaitOne();
            if (handler.Connected)//если передались данные о размере - можем принимать
            {
                //принимает сам пакет с данными
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
            }
        }
        /// <summary>
        /// Получаем размер файла
        /// </summary>
        /// <param name="ar"></param>
        public void ReadInfoCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket. 

            try
            {
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    string ssize = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                    // All the data has been read from the 
                    // client. Display it on the console.
                    WriteStatus("size=" + ssize);

                    try
                    {
                        state.sizePacket = long.Parse(ssize);
                        //sizePacket = long.Parse(ssize);
                    }
                    catch (Exception)
                    {
                        //не получилось преобразовать данные из строки(или тупо нет строки?)
                        throw;
                    }
                }

            }
            catch (Exception exc)
            {
                MessageBox.Show("Пропала связь при приеме размера данных. Error: " + exc.Message);
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
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try//смотрим не пропала ли связть(с помощью handler.EndReceive - SocketException
            {
                // Read data from the client socket. 
                int bytesRead = handler.EndReceive(ar);
                state.sizeReceived += bytesRead;
                if (bytesRead > 0)
                {
                    string currentPath = baseDir + "Data" + state.clientNum + ".bin";

                    BinaryWriter writer = new BinaryWriter(File.Open(currentPath, FileMode.Append));

                    writer.Write(state.buffer, 0, bytesRead);
                    writer.Close();

                    // All the data has been read from the 
                    // client. Display it on the console.
                    //WriteStatus("Read " + bytesRead + "  bytes from socket. Client " + state.clientNum);

                    // Echo the data back to the client.
                    // Send(handler, content);

                    //state.sb.Clear();
                    if (state.sizePacket != state.sizeReceived)
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            new AsyncCallback(ReadCallback), state);
                    }
                    else
                    {
                        WriteStatus("file was received");

                        state.sizeReceived = 0;

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

        public MainWindow()
        {
            InitializeComponent();
            //Пусть у нас простой случай - сервер запускается каждый раз с пустой папкой данных. (ничего не остается после выключения сервера)
            //Можно увязать логически с самой программой - клиент сразу подлючается к серу и отправляет данные
            //результат тоже получает в сеансе подключения=> нет нужды хранить данные дольше одного сеанса
            CreateEmptyDir();

            Task listen = Task.Factory.StartNew(StartListening);
        }

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

                    //считываем данные с полей
                    string login = LoginBox.Text;
                    string password = PasswordBox.Text;
                    if (login != string.Empty && password != string.Empty)
                    {
                        string result = "Login: " + login + "\r\n" + "Password: " + password + "\r\n";
                        //отправляем данные,закрывает подлючение
                        Send(currentClient.workSocket, result);

                        //ждем пока не завершится передача
                        sendDone.WaitOne();
                        // удаляем из списка и из комбо бокса
                        clients.Remove(currentClient);
                        clientComboBox.Items.Remove(clientComboBox.SelectedItem);
                    }
                    else
                    {
                        MessageBox.Show("Введите Login и Password");
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

        #region Отправляем данные нашему клиенту
        private void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                WriteStatus("Sent " + bytesSent + " bytes to client.");

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
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

        private void browseButton_Click(object sender, RoutedEventArgs e)
        {

            Process.Start("explorer.exe", baseDir);
        }
        
    }
}
