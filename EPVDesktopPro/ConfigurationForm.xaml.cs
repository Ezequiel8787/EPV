using ParserME;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;

using Application = System.Windows.Application;
using Path = System.IO.Path;

namespace EPVDesktopPro
{
    /// <summary>
    /// Interaction logic for ConfigurationForm.xaml
    /// </summary>
    public partial class ConfigurationForm : Window
    {
        Utils utils = new Utils();      //Función importada de la librería user32.dll para mostrar una ventana en diferentes estados

        [DllImport("user32.dll")]
        private static extern int FindWindow(string className, string windowText);
        [DllImport("user32.dll")]
        public static extern long ShowWindow(IntPtr hwnd, uint nCmdShow);

        //Función para pasar a primer plano una ventana y activarla
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        //Variable que indica que hay mas de una instancia de la app ejecutandoce
        private bool prev_instances = false;

        private NotifyIcon _notifyIcon;
        int hWnd = FindWindow("Shell_TrayWnd", "");
        /// <summary>
        /// Initializes a new instance of the ConfigurationForm class.
        /// </summary>
        public ConfigurationForm()
        {
            InitializeComponent();
            Topmost = true; // Coloca la ventana en la parte superior
        }
        /// <summary>
        /// Save configuration if user exists
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if the user has access
                if (utils.AccessUser(txtUsername1.Text, txtPassword1.Password))
                {
                    // Construct the database connection string
                    var conn = $"Server={txtServer.Text};Database={txtDataBase.Text};User={txtUsername.Text};Password={txtPassword.Password}";

                    // Get user roles for the provided username
                    var userInfo = utils.GetUserRoleByWindowsUser(txtUsername1.Text, conn);
                    int aux = 0;
                    foreach (DataRow user in userInfo.Rows)
                    {
                        // Check if the user has an admin or master admin role
                        if (user["Rol"].ToString() == "ADMIN" || user["Rol"].ToString() == "MASTER ADMIN")
                        {
                            aux++;
                            string configFilePath = ConfigurationManager.AppSettings["ConfigFilePath"];
                            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFilePath);

                            // Read the contents of the .ini file and store it in a dictionary
                            Dictionary<string, string> iniData = new Dictionary<string, string>();
                            string[] lines = File.ReadAllLines(fullPath);
                            foreach (string line in lines)
                            {
                                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(";"))
                                {
                                    string[] parts = line.Split(':');
                                    if (parts.Length == 2)
                                    {
                                        string key = parts[0].Trim();
                                        string value = parts[1].Trim();
                                        iniData[key] = value;
                                    }
                                }
                            }

                            // Update the values in the dictionary with the new values from the input fields
                            iniData["Server"] = utils.Encrypt(txtServer.Text);
                            iniData["Database"] = utils.Encrypt(txtDataBase.Text);
                            iniData["User"] = utils.Encrypt(txtUsername.Text);
                            iniData["Password"] = utils.Encrypt(txtPassword.Password.ToString());

                            // Save the updated contents back to the .ini file
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine($"[DataBase]");
                            foreach (KeyValuePair<string, string> entry in iniData)
                            {
                                sb.AppendLine($"{entry.Key}:{entry.Value}");
                            }
                            File.WriteAllText(fullPath, sb.ToString());

                            // Start the application with the updated configuration
                            MainWindow mainWindow = new MainWindow();
                            mainWindow.Show();
                            Application.Current.MainWindow = mainWindow;
                            this.Hide(); // Hide first
                            this.Close(); // Then close
                            break;
                        }
                    }

                    // If no admin or master admin role was found for the user
                    if (aux == 0)
                    {
                        System.Windows.Forms.MessageBox.Show("You don't have access");
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("User does not exist");
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error: {ex.Message}");
            }
        }


        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // Add this line
            Close();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // Add this line
            Close();
        }

    }
}
