using System;
using System.Collections.Generic;
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

namespace MyClient
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static int port = 11000;
        public static string ipAddress;

        static IPEndPoint remoteEP;
        static Socket client;

        private static byte[] packetSerialize;

        // ManualResetEvent instances signal completion.
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // The response from the remote device.
        private static String response = String.Empty;

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
                //StatusTextBox.Text += "IPendPoint was created, ip:" + remoteEP.Address.ToString() + " port: " + remoteEP.Port.ToString() + "\r\n";
                WriteStatus("IPendPoint was created, ip:" + remoteEP.Address.ToString() + " port: " + remoteEP.Port.ToString());

                // Create a TCP/IP socket.
                client = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);
                //StatusTextBox.Text += "Socket was created" + "\r\n";
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
        public void StartClient()
        {
            connectDone.Reset();
            if (ClientPrepare())//запускаем подготовку соединения. если все хорошо, выполняем код
            {
                try
                {
                    // Connect to the remote endpoint.
                    client.BeginConnect(remoteEP,
                            new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();//ждем пока не будет установлено подключение

                }
                catch (SocketException exc)
                {
                    MessageBox.Show(exc.Message);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message);
                }
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
                }
                else
                {
                    MessageBox.Show("have no data to receive");
                    // Get the rest of the data.
                    //client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    //    new AsyncCallback(ReceiveCallback), state);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        /// <summary>
        /// Вызывается когда выполнено подключение
        /// </summary>
        /// <param name="ar">Содержит результат асинхронного запроса(оболочка над данными)</param>
        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                // Получаем наш соект из результ.данных
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                // Закрываем соединение(поток)
                client.EndConnect(ar);

                //StatusTextBox.Text += "Socket connected to " + client.RemoteEndPoint.ToString() + "\r\n";
                WriteStatus("Socket connected to " + client.RemoteEndPoint.ToString());
            }
            catch (SocketException exc)
            {
                MessageBox.Show(exc.Message);
                //StatusTextBox.Text += exc.Message + "\r\n";
                client.Close();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
                //StatusTextBox.Text += exc.Message + "\r\n";
            }
            finally
            {
                // Signal that the connection has been made.
                // Сигнализируем основному потоку что можно продолжать.
                connectDone.Set();
            }
        }
        private UserInfo GetUserData()
        {
            UserInfo currentUser = new UserInfo();
            currentUser.FullName = FirstLastNameBox.Text;
            currentUser.University = UniversityBox.Text;
            currentUser.Phone = PhoneBox.Text;

            return currentUser;
        }
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
        private void SerializeMyPacket(MyPacketWrapper currentPacket)
        {
            //GZipStream zip = new GZipStream(ms, CompressionMode.Compress);
            //byte[] buffer = new byte[list.Capacity];
            //zip.Write(buffer, 0, buffer.Length);
            //на обратной стороне разжать данные!
            MemoryStream ms = new MemoryStream();
            BinaryFormatter serializer = new BinaryFormatter();

            serializer.Serialize(ms, currentPacket);

            packetSerialize = ms.ToArray();

            ms.Close();
            // StatusTextBox.Text += "Serialization completed, size:" + packetSerialize.Length + "\r\n";
            WriteStatus("Serialization completed, size:" + packetSerialize.Length);

        }

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

                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }
        void Shutdown()
        {
            //меняем название кнопки(не путать с действиями)
            ConnectButton.Content = "Подключиться";
            // Release the socket.
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }
        void StartUp()
        {
            if (IPBox.Text != string.Empty && PortBox.Text != string.Empty)//+ проверку на корректные данные
            {
                ConnectButton.Content = "Отключиться";

                ipAddress = IPBox.Text;
                port = int.Parse(PortBox.Text);

                StartClient();
            }
            else
            {
                MessageBox.Show("Введите IP адрес и порт");
            }
        }
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (client == null)
            {
                StartUp();
            }
            else
            {
                if (client.Connected)
                {
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
                //ConnectButton.Content=="Отключиться")
                //while (true)//кнопка отмены подключения
                //{
                if (FirstLastNameBox.Text != string.Empty
                    && UniversityBox.Text != string.Empty
                    && PhoneBox.Text != string.Empty)
                {
                    UserInfo user = GetUserData();//получает данные от пользователя//засунуть в конструктор?
                    PrepareData(user, FileNameTextBox.Text);

                    byte[] info = Encoding.ASCII.GetBytes(packetSerialize.Length.ToString());

                    Send(info);
                    Send(packetSerialize);
                    //StatusTextBox.Text += "All bytes have been sent." + "\r\n";
                    WriteStatus("All bytes have been sent.");

                    WriteStatus("Wating to answer...");
                    Task receiveTask = Task.Factory.StartNew(Receive);
                    // Receive the response from the remote device.
                    //Receive();
                    //receiveDone.WaitOne();

                }
                else
                {
                    MessageBox.Show("Заполните данные пользователя");
                }
                //}
            }
            else
            {
                MessageBox.Show("Сначала необходимо подключиться");
            }
        }

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

        public MainWindow()
        {
            InitializeComponent();
        }

    }
}
