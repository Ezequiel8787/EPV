using Encryption4_5;
using EPVDesktopPro.MaintenanceWS1;
using Newtonsoft.Json;
using ParserME;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;

namespace EPVDesktopPro
{


    /// <summary>
    /// Interaction logic for FormError.xaml
    /// </summary>
    public partial class FormError : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public string Message { get; set; }
        public string EquipmentName { get; set; }
        public int equipmentId { get; set; }
        public int StatusFormError { get; set; }
        public string Username { get; set; }
        public string Line { get; set; }
        public string Type { get; set; }
        public string FailureDate { get; set; }
        private string activeDirectory;
        private string accessList;
        Utils utils;
        MainWindow win;

        public FormError()
        {
            InitializeComponent();
            activeDirectory = Decrypt(ConfigurationManager.AppSettings["AD"]);
            accessList = ConfigurationManager.AppSettings["AccessList"];
            lblErrorLogin.Text = "";

        }
        /// <summary>
        /// Retrieves the user role from the database based on the Windows user.
        /// </summary>
        /// <param name="windowsUser">Windows user</param>
        /// <returns>True if the user role matches the specified type and equipment name, false otherwise</returns>
        public bool GetUserParser(string windowsUser)
        {
            try
            {
                Console.WriteLine($"DEBUG - Type: {Type}, EquipmentName: {EquipmentName}"); // Debug

                if (string.IsNullOrEmpty(EquipmentName))
                {
                    lblErrorLogin.Text = "Error: EquipmentName no está definido";
                    Console.WriteLine("DEBUG - EquipmentName está vacío"); // Debug
                    return false;
                }

                utils = new Utils();
                var connectionString = utils.connectionString;
                const string queryString = "exec [dbo].[GetUserRoleByWindowsUser] @WindowsUser";

                using (var cnn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(queryString, cnn))
                {
                    cmd.Parameters.AddWithValue("@WindowsUser", windowsUser);
                    cmd.CommandTimeout = 30;
                    cnn.Open();

                    Console.WriteLine($"DEBUG - Ejecutando query para usuario: {windowsUser}"); // Debug

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string readerType = reader["Type"].ToString();
                            string readerEquipmentName = reader["EquipmentName"].ToString();
                            
                            Console.WriteLine($"DEBUG - DB Values - Type: {readerType}, EquipmentName: {readerEquipmentName}"); // Debug
                            
                            if (readerType == Type && readerEquipmentName == EquipmentName)
                            {
                                return true;
                            }
                        }
                    }
                }

                lblErrorLogin.Text = $"Usuario no autorizado para equipo: {EquipmentName}";
                return false;
            }
            catch (Exception ex)
            {
                lblErrorLogin.Text = $"Error en validación: {ex.Message}";
                Console.WriteLine($"DEBUG - Error: {ex.Message}"); // Debug
                return false;
            }
        }

        /// <summary>
        /// Checks if the user has access and performs necessary operations.
        /// </summary>
        /// <returns>True if the user has access and the operations are successful, false otherwise</returns>
        public bool AccessClose()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    lblErrorLogin.Text = "Por favor complete todos los campos";
                    return false;
                }

                var result = GetUserParser(txtUsername.Text);
                if (!result)
                {
                    lblErrorLogin.Text = "Usuario no autorizado para esta operación";
                    return false;
                }

                if (txtComments.Text.Length < 20)
                {
                    lblErrorLogin.Text = "Los comentarios deben tener al menos 20 caracteres";
                    return false;
                }

                InsertLock(EquipmentName, Line, Message, txtComments.Text, txtUsername.Text);
                return true;
            }
            catch (Exception ex)
            {
                lblErrorLogin.Text = $"Error al verificar acceso: {ex.Message}";
                return false;
            }
        }
        /// <summary>
        /// Authenticates the user against the Active Directory.
        /// </summary>
        public void ActiveDirectoryAuthenticate()
        {
            try
            {
                // Validación de campos vacíos
                if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    lblErrorLogin.Text = "Por favor complete todos los campos de usuario y contraseña";
                    return;
                }

                // Validación de longitud mínima de comentarios
                if (txtComments.Text.Length < 20)
                {
                    lblErrorLogin.Text = "Los comentarios deben tener al menos 20 caracteres";
                    return;
                }

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = 
                    (sender, certificate, chain, sslPolicyErrors) => true;

                var apiUrl = activeDirectory;
                object input = new
                {
                    Username = txtUsername.Text.Trim(),
                    Password = txtPassword.Password.ToString().Trim()
                };

                try
                {
                    var inputJson = JsonConvert.SerializeObject(input);
                    var client = new WebClient
                    {
                        Headers = { ["Content-type"] = "application/json" },
                        Encoding = Encoding.UTF8
                    };
                    var json = client.UploadString(apiUrl, inputJson);
                    var data = JsonConvert.DeserializeObject<User>(json);

                    if (!AccessClose())
                    {
                        lblErrorLogin.Text = "Usuario no tiene permisos para esta operación";
                        return;
                    }

                    // Si todo está bien, proceder con el cierre
                    DeleteGenericValidation(equipmentId);
                    utils.InsertHistoryParser("", EquipmentName, "", "Close", "Success");
                    UpdateStatusParser(EquipmentName, Line, 0, "Running");
                    Close();
                }
                catch (WebException ex)
                {
                    if (ex.Response is HttpWebResponse response)
                    {
                        switch (response.StatusCode)
                        {
                            case HttpStatusCode.Unauthorized:
                                lblErrorLogin.Text = "Usuario o contraseña incorrectos";
                                break;
                            case HttpStatusCode.NotFound:
                                lblErrorLogin.Text = "Servicio de autenticación no disponible";
                                break;
                            default:
                                lblErrorLogin.Text = $"Error de conexión: {ex.Message}";
                                break;
                        }
                    }
                    else
                    {
                        lblErrorLogin.Text = "No se pudo conectar al servicio de autenticación";
                    }
                }
                catch (Exception ex)
                {
                    lblErrorLogin.Text = $"Error inesperado: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                lblErrorLogin.Text = $"Error en la aplicación: {ex.Message}";
            }

        }
        /// <summary>
        /// Updates the status of the parser in the database.
        /// </summary>
        /// <param name="equipmentName">Equipment name</param>
        /// <param name="line">Line</param>
        /// <param name="time">Time</param>
        /// <param name="status">Status</param>
        /// <returns>True if the status is updated successfully, false otherwise</returns>
        public bool UpdateStatusParser(string equipmentName, string line, int time, string status)
        {
            try
            {
                utils = new Utils();
                using (var connection = new SqlConnection(utils.connectionString))
                {
                    using (var command = new SqlCommand("exec [dbo].sp_UpdateStatusParser @EquipmentName, @Line, @Time, @Status", connection))
                    {
                        command.Parameters.AddRange(new SqlParameter[]
                        {
                        new SqlParameter("@EquipmentName", equipmentName),
                        new SqlParameter("@Line", line),
                        new SqlParameter("@Time", time),
                        new SqlParameter("@Status", status)
                        });

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            var dt = new DataTable();
                            adapter.Fill(dt);
                           
                        }
                    }
                }
            }
            catch (Exception e)
            {                 
                return false;            
            }
            return true;
        }
        /// <summary>
        /// Inserts a lock record into the database.
        /// </summary>
        /// <param name="equipmentName">Equipment name</param>
        /// <param name="line">Line</param>
        /// <param name="messageError">Error message</param>
        /// <param name="Username">Username</param>
        /// <returns>True if the lock record is inserted successfully, false otherwise</returns>
        public bool InsertLock(string equipmentName, string line, string messageError, string comments, string Username)
        {
            utils = new Utils();
            using (var connection = new SqlConnection(utils.connectionString))
            {
                using (var command = new SqlCommand("exec [dbo].[sp_InsertLock] @EquipmentName, @Line, @MessageError, @Comments, @UserName", connection))
                {
                    command.Parameters.AddRange(new SqlParameter[]
                    {
                        new SqlParameter("@EquipmentName", equipmentName),
                        new SqlParameter("@Line", line),
                        new SqlParameter("@MessageError", messageError),
                        new SqlParameter("@Comments", comments),
                        new SqlParameter("@UserName", Username)
                    });

                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var dt = new DataTable();
                        adapter.Fill(dt);
                        return true;
                    }
                }
            }
        }
        /// <summary>
        /// Deletes the generic validation for a specific equipment.
        /// </summary>
        /// <param name="equipmentId">Equipment ID</param>
        public void DeleteGenericValidation(int equipmentId)
        {
            var service = new MaintenanceWSSoapClient();
            service.DeleteGenericFailedValidations(equipmentId, "ParameterValidation");

        }

        private static string Decrypt(string strText)
        {
            Encryption encryption = new Encryption();
            var result = encryption.DecryptString(strText);
            return result;
        }

        private void Window_Activated(object sender, EventArgs e)
        {

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Deshabilitar botón cerrar (X) y Alt+F4
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

            TextRange textRange = new TextRange(txtError.Document.ContentStart, txtError.Document.ContentEnd);
            textRange.Text = Message;

            // Mostrar la fecha de falla
            lblFailureDate.Content = $"Fecha de falla: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

            //int equipmentId = utils.GetEquipmentMesId(EquipmentName);
            // UpdateStatusParser(EquipmentName, Line, 0, "Warning");
        }

        private void txtUsername_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ActiveDirectoryAuthenticate();
            }
        }

        private void txtPassword_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                ActiveDirectoryAuthenticate();
            }
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {

            ActiveDirectoryAuthenticate();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Solo permitir cerrar si el estado es Running
            var dt = utils.GetEquipmentByEquipmentName(EquipmentName);
            if (dt.Rows[0]["Status"].ToString() != "Running")
            {
                e.Cancel = true;
            }
            else
            {
                UpdateStatusParser(EquipmentName, Line, 0, "Running");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Prevenir Alt+F4
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(HookProc);
        }

        private IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;

            if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_CLOSE)
            {
                handled = true;
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        private void Window_Closed(object sender, EventArgs e)
        {

        }

        private void txtComments_KeyUp(object sender, KeyEventArgs e)
        {
            int charCount = txtComments.Text.Length;
            lblCharCount.Text = $"{charCount} characters";

            if (charCount < 20)
            {
                // Realizar acciones en caso de que el texto sea menor a 20 caracteres
            }
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public class User
    {
        public string FirstName { get; set; }
        public string Department { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string LastName { get; set; }
        public string Title { get; set; }
        public string EmployeeNum { get; set; }
        public IEnumerable<AdGroups> MemberGroups { get; set; }
    }

}
