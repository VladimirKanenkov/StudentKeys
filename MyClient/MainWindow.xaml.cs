using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MyClient
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Данные для подключения
        public static int port = 11000;
        public static string ipAddress;
        #endregion
        #region Прочие данные
        static IPEndPoint remoteEP;
        static Socket client;
        private static byte[] packetSerialize;//представляет наши сериализованные данные
        #endregion
        #region ManualResetEvent
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);
        #endregion
        #region Вспомогательные функции
        /// <summary>
        /// Выводит результаты в нашу псевдо-консоль при помощи Dispatcher
        /// </summary>
        /// <param name="info">Строка, которую необходимо отобразить</param>
        void WriteStatus(string info)
        {
            this.Dispatcher.BeginInvoke
                (DispatcherPriority.Normal,
                    (ThreadStart)delegate()
                    {
                        StatusTextBox.Text += info + "\r\n";
                    }
            );
        }
        /// <summary>
        /// Завершает подключение, выводит сообщение
        /// </summary>
        void Shutdown()
        {
            client.Shutdown(SocketShutdown.Both);
            client.Close();
            // client = null;
            WriteStatus("Scoket was closed");
        }
        /// <summary>
        /// Считывает данные с полей(+проверки). Запускает StartClient. Изменяет состояние кнопки ConnectButton
        /// </summary>
        void StartUp()
        {
            if (IPBox.Text != string.Empty && PortBox.Text != string.Empty)//+ проверку на корректные данные
            {
                ipAddress = IPBox.Text;
                port = int.Parse(PortBox.Text);

                bool result;
                result = StartClient();
                if (result == true)
                {
                    ConnectButton.Content = "Отключиться";
                }
                else
                {
                    ConnectButton.Content = "Подключиться";

                }
            }
            else
            {
                MessageBox.Show("Введите IP адрес и порт");
            }
        }
        #endregion
        #region Подключение - получение сокета
        /// <summary>
        /// Создаем наш сокет, получаем/определяем ип сервера, к которому подключаемся.
        /// </summary>
        private bool ClientPrepare()
        {
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(ipAddress);
                IPAddress IPAddr = ipHostInfo.AddressList[0];
                foreach (var addr in ipHostInfo.AddressList)
                {
                    if (!addr.IsIPv6LinkLocal)
                    {
                        IPAddr = addr;
                        break;
                    }
                }

                remoteEP = new IPEndPoint(IPAddr, port);
                WriteStatus("IPendPoint was created, ip:" + remoteEP.Address.ToString() + " port: " + remoteEP.Port.ToString());

                client = new Socket(AddressFamily.InterNetwork,
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
        /// Создается сокет, привязывается к адресу, и производится попытка подключения
        /// </summary>
        /// <returns>Результат операции</returns>
        public bool StartClient()
        {
            connectDone.Reset();
            if (ClientPrepare())//запускаем подготовку соединения. если все хорошо, выполняем код
            {
                try
                {
                    client.BeginConnect(remoteEP,
                            new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();//ждем пока не будет установлено подключение
                    if (client.Connected)
                    {
                        return true;
                    }
                    else
                    {
                        WriteStatus("Scoket was closed");
                        return false;
                    }
                }
                catch (SocketException exc)
                {
                    MessageBox.Show(exc.Message);
                    WriteStatus("Connection error");

                    return false;
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message);
                    return false;
                }
            }
            return false;
        }
        /// <summary>
        /// Вызывается когда выполнено подключение
        /// </summary>
        /// <param name="ar">Содержит результат асинхронного запроса(оболочка над данными)</param>
        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Получаем наш соект из результ.данных
                Socket client = (Socket)ar.AsyncState;

                // Закрываем соединение(поток)
                client.EndConnect(ar);

                WriteStatus("Socket was connected to " + client.RemoteEndPoint.ToString());
            }
            catch (SocketException exc)
            {
                MessageBox.Show(exc.Message);
                client.Close();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
            finally
            {
                // Сигнализируем основному потоку что можно продолжать.
                connectDone.Set();
            }
        }
        #endregion
        #region Работа с данными(подготовка, сериализация)
        /// <summary>
        /// Подготавливает данные для отпавки. Результат - byte[] packetSerialize
        /// </summary>
        /// <param name="user">Объект с информацией о пользователе</param>
        /// <param name="pathToFile">Путь к файлу в формате :</param>
        private void PrepareData(UserInfo user, string pathToFile)
        {
            MyPacketWrapper myPacket = new MyPacketWrapper();
            {
                try
                {
                    myPacket.FileBuff = File.ReadAllBytes(pathToFile);
                    myPacket.FileName = System.IO.Path.GetFileName(pathToFile); ;
                    myPacket.UserDetails = user;

                    SerializeMyPacket(myPacket);
                }
                catch (FileNotFoundException exc)
                {
                    MessageBox.Show(exc.Message);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message);
                }
            }
        }
        /// <summary>
        /// Сериализует тукущий объект. (в слудующих версиях возможно внесу в сам класс)
        /// </summary>
        /// <param name="currentPacket"></param>
        private void SerializeMyPacket(MyPacketWrapper currentPacket)
        {
            
            //Можно реализовать сжатие данных. В это случае придется "разжимать" данные на другой стороне
            //GZipStream zip = new GZipStream(ms, CompressionMode.Compress);
            //byte[] buffer = new byte[list.Capacity];
            //zip.Write(buffer, 0, buffer.Length);

            MemoryStream ms = new MemoryStream();
            BinaryFormatter serializer = new BinaryFormatter();
            serializer.Serialize(ms, currentPacket);
            packetSerialize = ms.ToArray();

            ms.Close();
            WriteStatus("Serialization completed, size:" + packetSerialize.Length);
        }
        #endregion
        #region Send/Receive
        private void Send(byte[] packetSerialize)
        {

            sendDone.Reset();
            receiveDone.Reset();
            try
            {
                client.BeginSend(packetSerialize, 0, packetSerialize.Length, 0,
                    new AsyncCallback(SendCallback), client);
                sendDone.WaitOne();
            }
            catch (SocketException exc)
            {
                MessageBox.Show(exc.Message);
                //WriteStatus("Socket was closed");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                // MessageBox.Show("Sent " +bytesSent+" bytes to server." );

                // Signal that all bytes has been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }
        private void Receive()
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                receiveDone.Set();
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket MyClient = state.workSocket;

                // Read data from the remote device.
                int bytesRead = MyClient.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    string result = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                    WriteStatus(result);

                    Shutdown();

                    this.Dispatcher.BeginInvoke
                        (DispatcherPriority.Normal,
                            (ThreadStart)delegate()
                            {
                                ConnectButton.Content = "Подключиться";
                            }
                    );
                    WriteStatus("Socket was disconnected\r\n");
                }
                else
                {
                    MessageBox.Show("have no data to receive");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        #endregion
        #region Buttons
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null||(client.Connected==false))
            {
                StartUp();
            }
            else
            {
                if (client.Connected)
                {
                    //Меняем надпись на кнопке
                    ConnectButton.Content = "Подключиться";

                    //Отправляем на сервер сообщение об отключении
                    byte[] message = Encoding.ASCII.GetBytes("DISCONNECT");
                    Send(message);
                    //завершаем соединение
                    Shutdown();
                }
                else
                {
                    StartUp();
                }
            }
        }
        /// <summary>
        /// Подготавливаем данные для отправки и отправляем
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.Connected)
            {
                    //проверяем заполненость полей
                if (FirstLastNameBox.Text != string.Empty
                    && UniversityBox.Text != string.Empty
                    && PhoneBox.Text != string.Empty)
                {
                    //получаем и подготавливаем данные
                    UserInfo user = new UserInfo(FirstLastNameBox.Text, UniversityBox.Text, PhoneBox.Text);
                    PrepareData(user, FileNameTextBox.Text);
                    byte[] info = Encoding.ASCII.GetBytes(packetSerialize.Length.ToString());

                    //отправляем информацию о размере(или об отключении)
                    Send(info);
                    if (client.Connected)
                    {
                        Send(packetSerialize);
                        WriteStatus("All bytes has been sent.");

                        WriteStatus("Wating to answer...");
                        Task receiveTask = Task.Factory.StartNew(Receive);
                    }
                    else
                    {
                        WriteStatus("Connection was closed");
                        ConnectButton.Content = "Подключиться";

                        Shutdown();
                    }
                }
                else
                {
                    MessageBox.Show("Заполните данные пользователя");
                }
            }
            else
            {
                MessageBox.Show("Сначала необходимо подключиться");
            }
        }
        /// <summary>
        /// Открывает диалоговое окно с выбором пути. Резултат путь, в текстовое поле FileNameTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void browseButton_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                FileNameTextBox.Text = dlg.FileName;
                MyPic.Source = new BitmapImage(new Uri(dlg.FileName, UriKind.RelativeOrAbsolute));
            }
        }
        #endregion
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
