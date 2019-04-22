using Client;
using SharedCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SimpleChat
{
    class MainWindowViewModel : ChangeNotifier
    {
        private SimpleClient _client;

        private bool _isAttemptingConnection;
        private bool _hasSetUsernameFromServer;
        private bool _isConnected;
        private string _chatText;
        private ObservableCollection<string> _users;
        private string _username;
        private string _lastServerUsername;
        private int _selectedUsernameIndex;

        private string _toggleConnectionButtonText;

        private string _serverIP;
        private bool _canChangeServerIP;
        private string _serverPort;
        private bool _canChangeServerPort;

        private string _messageToSend;

        public MainWindowViewModel()
        {
            // upgrading settings: https://stackoverflow.com/a/534335
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
            IsConnected = false;
            ToggleConnectionButtonText = "Connect";
            CanChangeServerIP = true;
            CanChangeServerPort = true;
            ChatText = "";
            Users = new ObservableCollection<string>();
            _isAttemptingConnection = false;
            _hasSetUsernameFromServer = false;
            SelectedUsernameIndex = -1;

            ServerIP = Properties.Settings.Default.LastUsedIP.ToString();
            ServerPort = Properties.Settings.Default.LastUsedPort.ToString();
        }

        public void Terminate()
        {
            _client?.Terminate();
            _client = null;
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            set { _isConnected = value; NotifyPropertyChanged(); }
        }

        public bool CanChangeServerIP
        {
            get { return _canChangeServerIP; }
            set { _canChangeServerIP = value; NotifyPropertyChanged(); }
        }

        public bool CanChangeServerPort
        {
            get { return _canChangeServerPort; }
            set { _canChangeServerPort = value; NotifyPropertyChanged(); }
        }

        public string ChatText
        {
            get { return _chatText; }
            set { _chatText = value; NotifyPropertyChanged(); }
        }

        public ObservableCollection<string> Users
        {
            get { return _users; }
            set { _users = value; NotifyPropertyChanged(); }
        }

        public string Username
        {
            get { return _username; }
            set { _username = value; NotifyPropertyChanged(); }
        }

        public string ToggleConnectionButtonText
        {
            get { return _toggleConnectionButtonText; }
            set { _toggleConnectionButtonText = value; NotifyPropertyChanged(); }
        }

        public string ServerIP
        {
            get { return _serverIP; }
            set { _serverIP = value; NotifyPropertyChanged(); }
        }

        public string ServerPort
        {
            get { return _serverPort; }
            set { _serverPort = value; NotifyPropertyChanged(); }
        }

        public string MessageToSend
        {
            get { return _messageToSend; }
            set { _messageToSend = value; NotifyPropertyChanged(); }
        }

        public int SelectedUsernameIndex
        {
            get { return _selectedUsernameIndex; }
            set { _selectedUsernameIndex = value; NotifyPropertyChanged(); }
        }

        public ICommand ToggleConnection
        {
            get { return new RelayCommand(ConnectOrDisconnect); }
        }

        private void RunOnUIThread(Action action)
        {
            Application.Current.Dispatcher.Invoke(action);
        }

        private void ConnectOrDisconnect()
        {
            if (IsConnected)
            {
                Terminate();
            }
            else
            {
                if (int.TryParse(ServerPort, out int port))
                {
                    Properties.Settings.Default.LastUsedIP = ServerIP;
                    Properties.Settings.Default.LastUsedPort = port;
                    Properties.Settings.Default.Save();
                    _isAttemptingConnection = true;
                    _client = new SimpleClient(ServerIP, port);
                    _client.NewMessage += ClientNewMessage;
                    _client.UserConnected += ClientUserConnected;
                    _client.TotalUsers += ClientTotalUsers;
                    _client.UserDisconnected += ClientUserDisconnected;

                    _client.SetUsername += ClientSetUsername;
                    _client.UsernameTaken += ClientUsernameTaken;
                    _client.UsernameChanged += ClientUsernameChanged;

                    _client.ConnectionConnect += ClientConnectedToServer;
                    _client.ConnectionDisconnect += ClientDisconnectedToServer;

                    _client.UserKicked += ClientUserKicked;
                }
            }
        }

        private void AddToChatText(string text)
        {
            if (!string.IsNullOrWhiteSpace(ChatText))
            {
                ChatText += "\n";
            }
            ChatText += text;
        }

        private void ClientUserKicked(string username)
        {
            if (username == Username)
            {
                Terminate();
                AddToChatText("[You were kicked from the server]");
            }
        }

        private void ClientUsernameTaken(string takenUsername)
        {
            AddToChatText(string.Format("[The username {0} is already taken. Please choose a different username.]", takenUsername));
        }

        private void ClientSetUsername(string username)
        {
            _lastServerUsername = username;
            if (string.IsNullOrWhiteSpace(Username) && _isAttemptingConnection)
            {
                Username = username;
            }
        }

        private void ClientUsernameChanged(string oldUsername, string changedUsername)
        {
            RunOnUIThread(() =>
            {
                Users.Remove(oldUsername);
                Users.Add(changedUsername);
            });
            AddToChatText("[" + oldUsername + " changed their username to " + changedUsername + "]");
            SortUserList();
        }

        private void SortUserList()
        {
            RunOnUIThread(() =>
            {
                Users = new ObservableCollection<string>(Users.OrderBy(i => i));
                UpdateSelectedUsernameIndex();
            });
        }

        private void ClientConnectedToServer()
        {
            IsConnected = true;
            ToggleConnectionButtonText = "Disconnect";
            CanChangeServerIP = false;
            CanChangeServerPort = false;
            AddToChatText("[Connected to server]");
            if (!string.IsNullOrWhiteSpace(Username) && _isAttemptingConnection && _hasSetUsernameFromServer) // TODO
            {
                SendChangedUsername();
            }
            else if (string.IsNullOrWhiteSpace(Username) && _isAttemptingConnection && _hasSetUsernameFromServer)
            {
                // if cleared the username field, then disconnected, field will be empty, so can't resend it
                Username = _lastServerUsername;
                SendChangedUsername();
            }
            _isAttemptingConnection = false;
            _hasSetUsernameFromServer = true;
            UpdateSelectedUsernameIndex();
        }

        private void UpdateSelectedUsernameIndex()
        {
            SelectedUsernameIndex = Users.IndexOf(Username);
        }

        private void ClientDisconnectedToServer()
        {
            RunOnUIThread(() =>
            {
                Users.Clear();
                _selectedUsernameIndex = -1;
            });
            IsConnected = false;
            ToggleConnectionButtonText = "Connect";
            CanChangeServerIP = true;
            CanChangeServerPort = true;
            AddToChatText("[Disconnected from server]");
            _isAttemptingConnection = false;
        }

        private void ClientUserConnected(string user)
        {
            AddToChatText("[" + user + " has connected]");
            RunOnUIThread(() =>
            {
                Users.Add(user);
                SortUserList();
            });
        }

        private void ClientUserDisconnected(string user)
        {
            AddToChatText("[" + user + " has disconnected]");
            RunOnUIThread(() =>
            {
                Users.Remove(user);
                UpdateSelectedUsernameIndex();
            });
        }

        private void ClientTotalUsers(IEnumerable<string> users)
        {
            RunOnUIThread(() =>
            {
                foreach (string user in users)
                {
                    Users.Add(user);
                }
                UpdateSelectedUsernameIndex();
            });
        }

        private void ClientNewMessage(string message, string sender)
        {
            AddToChatText("[" + sender + "]: " + message);
        }

        public ICommand SendMessage
        {
            get { return new RelayCommand(SendMessageToServer); }
        }

        private void SendMessageToServer()
        {
            if (!string.IsNullOrWhiteSpace(MessageToSend))
            {
                _client?.SendMessage(MessageToSend);
                ClearMessageCurrentlyBeingTyped();
            }
        }

        public ICommand ClearMessage
        {
            get { return new RelayCommand(ClearMessageCurrentlyBeingTyped); }
        }

        private void ClearMessageCurrentlyBeingTyped()
        {
            MessageToSend = "";
        }

        public ICommand SetUsername
        {
            get { return new RelayCommand(SendChangedUsername); }
        }

        private void SendChangedUsername()
        {
            if (!string.IsNullOrWhiteSpace(Username))
            {
                _client?.Send(MessageProtocols.SetUsername, true, Username);
            }
        }

        public ICommand ClearChatHistory
        {
            get { return new RelayCommand(EraseChatHistory); }
        }

        private void EraseChatHistory()
        {
            ChatText = "";
        }
    }
}
