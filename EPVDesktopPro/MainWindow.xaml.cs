
using CefSharp.DevTools.Database;
using MaxMind.Db;
using Newtonsoft.Json.Linq;
using ParserME;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using WpfAnimatedGif;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.Forms.MessageBox;
using Path = System.IO.Path;

namespace EPVDesktopPro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : System.Windows.Window
    {
        public string pathLog { get; set; }
        public string ErrorMessage { get; set; }
        public string Status { get; set; }
        public int TotalRecordFiles { get; set; }

        private string ConnectionStringMes;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 1;
        Utils utils;
        [DllImport("user32.dll")]
        private static extern int FindWindow(string className, string windowText);
        [DllImport("user32.dll")]
        private static extern int ShowWindow(int hwnd, int command);

        ObservableCollection<Result> results = new ObservableCollection<Result>();
        DispatcherTimer timer1 = new DispatcherTimer();
        private DispatcherTimer dailyResetTimer;

        //Function imported from the user32.dll library to display a window in different states
        [DllImport("user32.dll")]
        public static extern long ShowWindow(IntPtr hwnd, uint nCmdShow);

        //Function to bring a window to the foreground and activate it
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        //Variable indicating that there is more than one instance of the app executing
        private bool prev_instances = false;
        private NotifyIcon _notifyIcon;
        private bool isFormErrorOpen = false;
        private FormError frmError;
        public string fullPath { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            utils = new Utils();
            try
            {
                GetLines();
            }
            catch (Exception e)
            {
                lblServer.Document.Blocks.Clear();
                lblServer.AppendText(e.Message);
            }

            btnPlay.Text = "Waiting";
            cmbProgramName.SelectionChanged += cmbProgramName_SelectionChanged;
            if (utils.Line != "")
            {
                cmbLine.SelectedValue = utils.Line;
            }
            else
            {
                cmbLine.SelectionChanged += cmbLine_SelectionChanged;
                ConfigurationForm configurationForm = new ConfigurationForm();
                configurationForm.Show();
            }
            cmbProgramName.SelectedValue = utils.equipmentName;
            this.StateChanged += MainWindow_StateChanged;
            timer1.Tick += (sender, e) => timer1_Tick(sender, e);
            timer1.Interval = new TimeSpan(0, 0, 15);

      
        }      

        /// <summary>
        /// Checks the status of the equipment and displays error messages if necessary.
        /// </summary>
        /// <param name="equipmentName">Equipment name</param>
        /// <param name="barcode">Barcode</param>
        public void checkStatus(string equipmentName, string barcode)
        {
            try
            {

                DataTable historyStatus = utils.GetHistoryParserByEquipment(lbltitle.Text);
                if (historyStatus.Rows.Count > 0)
                {
                    if (historyStatus.Rows[0]["Status"].ToString() == "Fail")
                    {
                        if (!IsFormErrorOpen())
                        {
                            frmError = new FormError();
                            frmError.Closed += (sender, args) => isFormErrorOpen = false;
                            UpdateStatusParser(equipmentName, txtLine.Text, 0, "Warning");
                            frmError.Message = historyStatus.Rows[0]["MessageError"].ToString();
                            frmError.Type = txtType.Text;
                            frmError.EquipmentName = lbltitle.Text;
                            frmError.equipmentId = int.Parse(historyStatus.Rows[0]["Equipment_ID"].ToString());
                            frmError.Line = txtLine.Text;
                            frmError.Show();
                            isFormErrorOpen = true;
                            results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + historyStatus.Rows[0]["MessageError"].ToString(), Date = DateTime.Now.ToString() });
                            tblResults.CanUserResizeColumns = true;
                            tblResults.ItemsSource = results;
                            var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                            tblResults.ScrollIntoView(lastItem);
                        }
                        else
                        {
                            FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();
                            if (formSecundario != null)
                            {
                                UpdateStatusParser(equipmentName, txtLine.Text, 0, "Warning");
                                formSecundario.EquipmentName = lbltitle.Text;
                                if (historyStatus.Rows[0]["MessageError"].ToString() != formSecundario.Message)
                                {
                                    formSecundario.Message = historyStatus.Rows[0]["MessageError"].ToString();

                                    formSecundario.txtError.AppendText("\n" + historyStatus.Rows[0]["MessageError"].ToString());
                                    formSecundario.Activate();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                results.Add(new Result { SerialNumber = "N/A", Status = "F", ResultMessage = e.Message, Date = DateTime.Now.ToString() });
                tblResults.CanUserResizeColumns = true;
                tblResults.ItemsSource = results;
                // Get the reference to the last object in the data collection of the DataGrid
                var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                // Call the ScrollIntoView method to make the scroll move to the last item
                tblResults.ScrollIntoView(lastItem);
                ProcessEvents();
                TextRange textRange = new TextRange(lblServer.Document.ContentStart, lblServer.Document.ContentEnd);
                lblServer.Document.Blocks.Clear();
                textRange.Text = "Network connection failed";
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                NotifyIcon notifyIcon = new NotifyIcon();
                notifyIcon.Icon = new System.Drawing.Icon("pngaaa.com-4781446.ico");
                notifyIcon.Visible = true;
                notifyIcon.DoubleClick += (s, args) => ShowWindow();
                notifyIcon.DoubleClick += (s, args) => notifyIcon.Dispose();
            }
        }
        /// <summary>
        /// Shows the window and sets its state to Normal.
        /// </summary>
        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        /// <summary>
        /// Event handler for the timer tick event.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            lblServer.Document.Blocks.Clear();
            DataTable dt = new DataTable();
            try
            {
                if (string.IsNullOrEmpty(txtType.Text))
                {
                    return;
                }
                const string WAVE_SOLDER = "WAVE SOLDER";
                const string PVA = "PVA";
                const string INTERFLUX_COORDS = "INTERFLUX_COORDS";
                const string OVEN = "OVEN";
                const string SELECTIVA_COORDS = "SELECTIVA_COORDS";
                const string REHM = "REHM";
                const string KY = "KOH YOUNG";

                string equipmentName = lbltitle.Text;
                string line = txtLine.Text;

                switch (txtType.Text)
                {
                    case WAVE_SOLDER:
                        ImageBehavior.SetRepeatBehavior(ImgGif, RepeatBehavior.Forever);
                        btnStart.Background = Brushes.Green;
                        btnPlay.Text = "Running";
                        Status = "Running";
                        readLogWS(txtPathLogFile.Text.EndsWith("\\") ? txtPathLogFile.Text : txtPathLogFile.Text + "\\");
                        break;
                    case PVA:
                        ImageBehavior.SetRepeatBehavior(ImgGif, RepeatBehavior.Forever);
                        btnStart.Background = Brushes.Green;
                        btnPlay.Text = "Running";
                        Status = "Running";
                        readLogPVA(txtPathLogFile.Text.EndsWith("\\") ? txtPathLogFile.Text : txtPathLogFile.Text + "\\");
                        break;
                    case INTERFLUX_COORDS:
                        ImageBehavior.SetRepeatBehavior(ImgGif, RepeatBehavior.Forever);
                        btnStart.Background = Brushes.Green;
                        btnPlay.Text = "Running";
                        Status = "Running";
                        readCoordsInterflux(txtBackupFie.Text.EndsWith("\\") ? txtPathLogFile.Text : txtPathLogFile.Text + "\\");
                        break;
                    case OVEN:
                        ImageBehavior.SetRepeatBehavior(ImgGif, RepeatBehavior.Forever);
                        btnStart.Background = Brushes.Green;
                        btnPlay.Text = "Running";
                        Status = "Running";
                        readLogOven(txtPathLogFile.Text.EndsWith("\\") ? txtPathLogFile.Text : txtPathLogFile.Text + "\\");
                        break;
                    case SELECTIVA_COORDS:
                        ImageBehavior.SetRepeatBehavior(ImgGif, RepeatBehavior.Forever);
                        btnStart.Background = Brushes.Green;
                        btnPlay.Text = "Running";
                        Status = "Running";
                        readCoords(txtPathLogFile.Text.EndsWith("\\") ? txtPathLogFile.Text : txtPathLogFile.Text + "\\", txtBackupFileCoords.Text);
                        break;
                    case REHM:
                        ImageBehavior.SetRepeatBehavior(ImgGif, RepeatBehavior.Forever);
                        btnStart.Background = Brushes.Green;
                        btnPlay.Text = "Running";
                        Status = "Running";
                        readREHMAsync(txtPathLogFile.Text.EndsWith("\\") ? txtPathLogFile.Text : txtPathLogFile.Text + "\\", txtBackupFileCoords.Text);
                        break;
                    case KY:
                        ImageBehavior.SetRepeatBehavior(ImgGif, RepeatBehavior.Forever);
                        btnStart.Background = Brushes.Green;
                        btnPlay.Text = "Running";
                        Status = "Running";
                        readKY(txtPathLogFile.Text.EndsWith("\\") ? txtPathLogFile.Text : txtPathLogFile.Text + "\\", txtBackupFileCoords.Text);
                        break;
                    default:
                        // Mostrar un mensaje de error o hacer algo similar
                        break;
                }

                dt = utils.GetEquipmentByEquipmentName(equipmentName);
                if (dt != null && dt.Rows.Count > 0)
                {
                    string status = dt.Rows[0]["Status"].ToString();
                    if (status == "Running" || status == "Warning")
                    {
                        Status = status;
                        if (UpdateStatusParser(equipmentName, line, 0, status))
                        {

                        }
                        else
                        {
                            lblServer.Document.Blocks.Clear();
                            lblServer.AppendText("Falla al insertar status");
                        }
                    }
                    else
                    {
                        if (UpdateStatusParser(equipmentName, line, 0, "Running"))
                        {

                        }
                        else
                        {
                            lblServer.Document.Blocks.Clear();
                            lblServer.AppendText("Falla al insertar status");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("No se pudo obtener información del equipo en la base de datos.");
                }
                // Obtener la hora actual
                DateTime horaActual = DateTime.Now;

                // Establecer el minuto en 0
                DateTime horaInicio = new DateTime(horaActual.Year, horaActual.Month, horaActual.Day, horaActual.Hour, 0, 0);

                // Obtener la hora siguiente
                DateTime horaSiguiente = horaInicio.AddHours(1);

                // Obtener el texto del rango de horas
                string textoRangoHoras = "Data " + horaInicio.ToString("hh:mm tt") + " to " + horaSiguiente.ToString("hh:mm tt");

                // Imprimir el texto del rango de horas
                Console.WriteLine(textoRangoHoras);
            }
            catch (Exception we)
            {
                Console.WriteLine(we.Message);
                lblServer.Document.Blocks.Clear();
                lblServer.AppendText(we.Message);
            }
            finally
            {
                if (dt != null)
                {
                    dt.Dispose();
                }
            }
        }


        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private bool IsMaximized = false;
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (IsMaximized)
                {
                    WindowState = WindowState.Normal;
                    Width = 1080;
                    Height = 720;
                    IsMaximized = false;
                }
                else
                {
                    WindowState = WindowState.Maximized;
                    IsMaximized = true;
                }
            }
        }

        /// <summary>
        /// Retrieves the parameters for the specified REHM program and equipment.
        /// </summary>
        /// <param name="programName">Program name</param>
        /// <param name="equipmentName">Equipment name</param>
        /// <returns>DataTable containing the parameters</returns>
        public DataTable GetParametersREHM(string programName, string equipmentName)
        {
            var dt = new DataTable();
            const string queryString = "exec [dbo].[sp_GetParametersREHM] @EquipmentName, @ProgramName";
            using (var cnn = new SqlConnection(utils.connectionString))
            using (var cmd = new SqlCommand(queryString, cnn))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.Add("@EquipmentName", SqlDbType.NVarChar).Value = equipmentName;
                cmd.Parameters.Add("@ProgramName", SqlDbType.NVarChar).Value = programName;
                cmd.CommandTimeout = 30;
                da.Fill(dt);
            }
            return dt;
        }

        /// <summary>
        /// Retrieves the parameters for the specified oven program and equipment.
        /// </summary>
        /// <param name="programName">Program name</param>
        /// <param name="equipmentName">Equipment name</param>
        /// <returns>DataTable containing the parameters</returns>
        public DataTable GetParametersOven(string programName, string equipmentName)
        {
            var dt = new DataTable();
            const string queryString = "exec [dbo].[sp_GetParametersOven] @EquipmentName, @ProgramName";
            using (var cnn = new SqlConnection(utils.connectionString))
            using (var cmd = new SqlCommand(queryString, cnn))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.Add("@EquipmentName", SqlDbType.NVarChar).Value = equipmentName;
                cmd.Parameters.Add("@ProgramName", SqlDbType.NVarChar).Value = programName;
                cmd.CommandTimeout = 30;
                da.Fill(dt);
            }
            return dt;
        }
        /// <summary>
        /// Updates the status of the parser for the specified equipment.
        /// </summary>
        /// <param name="equipmentName">Equipment name</param>
        /// <param name="line">Line</param>
        /// <param name="time">Time</param>
        /// <param name="status">Status</param>
        /// <returns>True if the status was updated successfully, false otherwise</returns>
        public bool UpdateStatusParser(string equipmentName, string line, int time, string status)
        {
            try
            {
                using (var connection = new SqlConnection(utils.connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand("exec [dbo].sp_UpdateStatusParser @EquipmentName, @Line, @Time, @Status", connection))
                    {
                        command.Parameters.AddWithValue("@EquipmentName", equipmentName);
                        command.Parameters.AddWithValue("@Line", line);
                        command.Parameters.AddWithValue("@Time", time);
                        command.Parameters.AddWithValue("@Status", status);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// Updates the status of the parser for the specified equipment.
        /// </summary>
        /// <param name="equipmentName">Equipment name</param>
        /// <param name="line">Line</param>
        /// <param name="time">Time</param>
        /// <param name="status">Status</param>
        /// <returns>True if the status was updated successfully, false otherwise</returns>
        public DataTable GetParametersWS(string programName, string equipmentName)
        {
            var dt = new DataTable();
            const string queryString = "exec [dbo].sp_GetParametersWS @EquipmentName, @ProgramName";
            using (var cnn = new SqlConnection(utils.connectionString))
            using (var cmd = new SqlCommand(queryString, cnn))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.Add("@EquipmentName", SqlDbType.NVarChar).Value = equipmentName;
                cmd.Parameters.Add("@ProgramName", SqlDbType.NVarChar).Value = programName.Substring(0, programName.Length - 4);
                cmd.CommandTimeout = 30;
                da.Fill(dt);
            }
            return dt;
        }
        /// <summary>
        /// Checks if the given value is a float or an integer.
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <returns>True if the value is a float or an integer, false otherwise</returns>
        public bool IsFloatOrInt(string value)
        {
            int intValue;
            float floatValue;
            return Int32.TryParse(value, out intValue) || float.TryParse(value, out floatValue);
        }
        /// <summary>
        /// Reads coordinates from the specified log file path.
        /// </summary>
        /// <param name="pathLog">The path to the log file.</param>
        private void readCoordsInterflux(string pathLog)
        {
            string library = "";
            string programName = "";
            string barcode = "";
            string equipmentName = "";
            string messageError = "";
            string line = "";
            try
            {
                timer1.Stop();
                var controller = ImageBehavior.GetAnimationController(ImgGif);
                var files = Directory.GetFiles(pathLog);
                foreach (var file in files)
                {
                    equipmentName = lbltitle.Text;
                    line = txtLine.Text;
                    DirectoryInfo info = new DirectoryInfo(file);
                    DataTable dt = new DataTable();
                    library = "";
                    int aux = 0;
                    string csvfilePath = info.FullName.EndsWith("\\") ? info.Parent.FullName.ToString() + info.Name : info.Parent.FullName.ToString() + "\\" + info.Name;
                    String URLString = csvfilePath;
                    var dr = dt.NewRow();
                    int auxErrorFile = 0;
                    int equipmentId = utils.GetEquipmentMesId(lbltitle.Text);
                    if (info.Extension.ToUpper() == ".CSV")
                    {
                        string csvfilePath1 = info.FullName.EndsWith("\\") ? info.Parent.FullName.ToString() + info.Name : info.Parent.FullName.ToString() + "\\" + info.Name;
                        DataSet dataSet = new DataSet();
                        dataSet = CSVToDataTableInterlux(csvfilePath1);
                        DataTable dtParameters = new DataTable();
                        dtParameters = dataSet.Tables[4];
                        DataTable dtProgram = new DataTable();
                        dtProgram = dataSet.Tables[1];

                        int parserId = 0;
                        var dtParser = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                        if (dtParser.Rows.Count > 0)
                        {
                            foreach (DataRow p in dtParser.Rows)
                            {
                                parserId = int.Parse(p["Parser_ID"].ToString());
                                equipmentId = int.Parse(p["Equipment_ID"].ToString());
                            }
                        }

                        if (dtProgram.Rows.Count > 0)
                        {
                            foreach (DataRow p in dtProgram.Rows)
                            {
                                programName = p["Program name"].ToString();
                                barcode = p["Board counter"].ToString();
                            }
                        }
                        string error = "";
                        if (programName == "")
                        {
                            messageError = "The program not found in the system";
                            results.Add(new Result { SerialNumber = "N/A", Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });
                            tblResults.CanUserResizeColumns = true;
                            tblResults.ItemsSource = results;
                            // Obtén la referencia al último objeto en la colección de datos del DataGrid
                            var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                            // Llama al método ScrollIntoView para que el scroll se mueva al último item
                            tblResults.ScrollIntoView(lastItem);
                            ProcessEvents();

                            error += messageError;

                            auxErrorFile++;
                        }
                        string nameBackupFile = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                        var dtdb = GetCoordsInterflux(programName);
                        var auxError = 0;
                        if (dtdb.Rows.Count == 0)
                        {
                            messageError = "The program not found in the system";
                            results.Add(new Result { SerialNumber = "N/A", Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });
                            tblResults.CanUserResizeColumns = true;
                            tblResults.ItemsSource = results;
                            // Obtén la referencia al último objeto en la colección de datos del DataGrid
                            var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                            // Llama al método ScrollIntoView para que el scroll se mueva al último item
                            tblResults.ScrollIntoView(lastItem);
                            ProcessEvents();
                            utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", messageError);
                            error += messageError;
                            string nameBackupFile1 = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                            File.Copy(csvfilePath, txtBackupFie.Text.EndsWith("\\") ? txtBackupFie.Text + nameBackupFile1 : txtBackupFie.Text + "\\" + nameBackupFile1);
                            File.Delete(csvfilePath);
                            auxErrorFile++;
                        }
                        else
                        {
                            string nameBackupFile1 = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                            File.Copy(csvfilePath, txtBackupFie.Text.EndsWith("\\") ? txtBackupFie.Text + nameBackupFile1 : txtBackupFie.Text + "\\" + nameBackupFile1);
                            File.Delete(csvfilePath);
                            var tableColumnNames = dtParameters.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                            var schemaColumnNames = dtdb.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                            var unmatchedColumnNamesf =
                                from col in tableColumnNames where !schemaColumnNames.Contains(col) select col;
                            var unmatchedColumnNamesdb =
                                from col in schemaColumnNames where !tableColumnNames.Contains(col) select col;
                            foreach (var item in unmatchedColumnNamesf)
                            {
                                dtParameters.Columns.Remove(item);
                            }
                            foreach (var item in unmatchedColumnNamesdb)
                            {
                                dtdb.Columns.Remove(item);
                            }
                            // Obtener el orden de las columnas del primer DataTable
                            var order = dtParameters.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                            // Ordenar las columnas del segundo DataTable según el orden del primer DataTable
                            dtdb = new DataView(dtdb).ToTable(false, order.ToArray());

                            auxError = 0;
                            if (dtdb.Columns.Count > 0 && dtParameters.Columns.Count > 0)
                            {
                                DataTable dt3 = dtParameters.Clone();
                                dt3.Columns.Add("status");
                                int auxrow = 0;


                                if (dtParameters.Rows.Count < dtdb.Rows.Count)
                                {
                                    int totalrowsdiff = dtdb.Rows.Count - dtParameters.Rows.Count;
                                    DataRow drdb;
                                    for (int i = 0; i < totalrowsdiff; i++)
                                    {
                                        drdb = dtParameters.NewRow();
                                        dtParameters.Rows.Add(drdb);
                                    }
                                }

                                if (dtParameters.Rows.Count >= dtdb.Rows.Count)
                                {
                                    int totalrowsdiff = dtParameters.Rows.Count - 1 - dtdb.Rows.Count;
                                    DataRow drdb;
                                    for (int i = 0; i < totalrowsdiff; i++)
                                    {
                                        drdb = dtdb.NewRow();
                                        dtdb.Rows.Add(drdb);
                                    }
                                    for (int f = 0; f < dtParameters.Rows.Count - 1; f++)
                                    {
                                        int flagError = 0;
                                        foreach (DataColumn coldb in dtdb.Columns)
                                        {
                                            if (dtParameters.Rows[f]["Item"].ToString().ToUpper().Trim() == dtdb.Rows[f]["Item"].ToString().ToUpper().Trim())
                                            {
                                                if (coldb.ColumnName == "Type" || coldb.ColumnName == "Type")
                                                {
                                                    if (dtParameters.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim() == dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim())
                                                    {
                                                        auxrow++;
                                                    }
                                                    else
                                                    {
                                                        messageError += $"Row: {f + 1}, Col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtParameters.Rows[f][coldb.ColumnName]}\n";
                                                        auxError++;
                                                        flagError++;
                                                        Console.WriteLine($"Row: {f + 1}, Col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtParameters.Rows[f][coldb.ColumnName]}\n");
                                                    }
                                                }
                                                else
                                                {
                                                    bool result = IsFloatOrInt(dtParameters.Rows[f][coldb.ColumnName].ToString());
                                                    bool result1 = IsFloatOrInt(dtdb.Rows[f][coldb.ColumnName].ToString());
                                                    if (result && result1)
                                                    {
                                                        if (float.Parse(dtParameters.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) >= float.Parse(dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) - 1 &&
                                                               float.Parse(dtParameters.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) <= float.Parse(dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) + 1)
                                                        {
                                                            auxrow++;
                                                        }
                                                        else
                                                        {
                                                            messageError += $"Row: {f + 1}, Col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtParameters.Rows[f][coldb.ColumnName]}\n";
                                                            auxError++;
                                                            flagError++;
                                                            Console.WriteLine($"Row: {f + 1}, Col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtParameters.Rows[f][coldb.ColumnName]}\n");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (dtParameters.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim() == dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim())
                                                        {
                                                            auxrow++;
                                                        }
                                                        else
                                                        {
                                                            messageError += $"Row: {f + 1}, Col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtParameters.Rows[f][coldb.ColumnName]}\n";
                                                            auxError++;
                                                            flagError++;
                                                            Console.WriteLine($"Row: {f + 1}, Col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtParameters.Rows[f][coldb.ColumnName]}\n");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (auxError > 0)
                                    {
                                        //SmartFactory
                                        var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                        if (dtGeneralSettings.Rows.Count > 0)
                                        {
                                            if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                                            {
                                                if (equipmentId == 0)
                                                {
                                                    MessageBox.Show("No se encontro Id del equipo");
                                                    timer1.Stop();
                                                    return;
                                                }
                                                else
                                                {
                                                    InsertGenericValidation(equipmentId, "ParameterValidation", "Limits out range");
                                                }
                                            }
                                        }
                                        results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });
                                        tblResults.CanUserResizeColumns = true;
                                        tblResults.ItemsSource = results;
                                        // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                        var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                                        // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                        tblResults.ScrollIntoView(lastItem);
                                        ProcessEvents();
                                        utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", messageError);
                                        error += messageError;
                                        messageError = "";
                                        auxErrorFile++;
                                    }
                                    else
                                    {
                                        results.Add(new Result { SerialNumber = barcode, Status = "P", ResultMessage = "Pass", Date = DateTime.Now.ToString() });
                                        tblResults.CanUserResizeColumns = true;
                                        tblResults.ItemsSource = results;
                                        // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                        var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                                        // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                        tblResults.ScrollIntoView(lastItem);
                                        ProcessEvents();
                                        utils.InsertHistoryParser(barcode, equipmentName, programName, "Pass", "Success");
                                    }
                                }
                            }
                            else
                            {
                                timer1.Stop();
                                btnStart.Background = Brushes.Gold;
                                btnPlay.Text = "Waiting";
                                Status = "Waiting";
                                Console.WriteLine($"The coordinates log file contains more lines than the ones configured in the database.");
                                return;
                            }
                        }
                        if (auxErrorFile > 0)
                        {
                            if (!IsFormErrorOpen())
                            {
                                frmError = new FormError();
                                frmError.Closed += (sender, args) => isFormErrorOpen = false;
                                auxError = 0;
                                UpdateStatusParser(equipmentName, line, 0, "Warning");
                                frmError.Message += error;
                                frmError.Type = txtType.Text;
                                frmError.EquipmentName = equipmentName;
                                frmError.Line = line;
                                frmError.Show();
                                isFormErrorOpen = true;
                            }
                            else
                            {
                                // Buscar el formulario FormSecundario en las ventanas abiertas
                                FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                                if (formSecundario != null)
                                {
                                    // Acceder al RichTextBox en FormSecundario y agregar el texto
                                    formSecundario.txtError.AppendText("\n" + error);
                                    UpdateStatusParser(equipmentName, line, 0, "Warning");
                                }
                                formSecundario.Activate();
                            }
                        }
                    }
                    tblResults.CanUserResizeColumns = true;
                    tblResults.ItemsSource = results;
                }

            }
            catch (Exception e)
            {
                if (!IsFormErrorOpen())
                {
                    frmError = new FormError();
                    frmError.Closed += (sender, args) => isFormErrorOpen = false;

                    UpdateStatusParser(equipmentName, line, 0, "Warning");
                    frmError.Message += e.Message;
                    frmError.Type = txtType.Text;
                    frmError.EquipmentName = equipmentName;
                    frmError.Line = line;
                    frmError.Show();
                    isFormErrorOpen = true;
                }
                else
                {
                    // Buscar el formulario FormSecundario en las ventanas abiertas
                    FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                    if (formSecundario != null)
                    {
                        // Acceder al RichTextBox en FormSecundario y agregar el texto
                        formSecundario.txtError.AppendText("\n" + e.Message);
                    }
                    formSecundario.Activate();
                }
            }
            timer1.Start();
        }
        /// <summary>
        /// Retrieves coordinate data from the Interflux database for a given program.
        /// </summary>
        /// <param name="programName">The name of the program.</param>
        /// <returns>A DataTable containing the coordinate data.</returns>
        public DataTable GetCoordsInterflux(string programName)
        {
            DataTable dataTable = new DataTable();
            try
            {
                SqlConnection cnn = new SqlConnection(utils.connectionString);
                SqlCommand cmd = cnn.CreateCommand();
                cmd.CommandText = "exec [dbo].sp_GetCoordsInterflux @ProgramName";
                cmd.Parameters.AddWithValue("@ProgramName", programName);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.SelectCommand.CommandTimeout = 30;

                cnn.Open();
                DataTable dt = new DataTable();
                da.Fill(dt);
                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow d in dt.Rows)
                    {
                        string columnName = d["ColumnName"].ToString();
                        if (!dataTable.Columns.Contains(columnName))
                        {
                            dataTable.Columns.Add(columnName);
                        }
                    }
                    dataTable.Columns.Add("Set");

                    int lastRow = int.Parse(dt.Rows.Cast<DataRow>().Select(s => s["Row"].ToString()).LastOrDefault());
                    for (int i = 0; i <= lastRow; i++)
                    {
                        DataRow dr = dataTable.NewRow();
                        dataTable.Rows.Add(dr);
                    }

                    foreach (DataRow d in dt.Rows)
                    {
                        int rowIndex = int.Parse(d["Row"].ToString()) - 1;
                        string columnName = d["ColumnName"].ToString();
                        string value = d["Value"].ToString();

                        if (columnName == "Type")
                        {
                            dataTable.Rows[rowIndex][columnName] = d["Type"].ToString();
                            dataTable.Rows[rowIndex]["Set"] = d["Row"].ToString();
                        }
                        else
                        {
                            dataTable.Rows[rowIndex][columnName] = value;
                            dataTable.Rows[rowIndex]["Set"] = d["Row"].ToString();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                // Handle exception
                // Log error message
                // ...
            }

            return dataTable;
        }

        /// <summary>
        /// Create datatable from xml
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="line"></param>
        /// <param name="equipmentName"></param>
        /// <param name="programm"></param>
        /// <param name="library"></param>
        /// <returns></returns>
        public DataTable XmltoDatatable(string xml, string line, string equipmentName, string programm, string library)
        {
            // Cargar el XML en un objeto XmlDocument
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            // Crear una tabla para almacenar los datos
            DataTable table = new DataTable();
            table.Columns.Add("Line", typeof(string));
            table.Columns.Add("Equipment", typeof(string));
            table.Columns.Add("Library", typeof(string));
            table.Columns.Add("Program", typeof(string));
            table.Columns.Add("Type", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("CurrentValue", typeof(string));
            table.Columns.Add("Unit", typeof(string));
            table.Columns.Add("SetPoint", typeof(string));
            table.Columns.Add("ToleranceUp", typeof(string));
            table.Columns.Add("ToleranceDown", typeof(string));
            table.Columns.Add("LastUpdated", typeof(string));
            // Obtener todos los elementos "LoetProtElement" del XML
            XmlNodeList elements = doc.SelectNodes("//LoetProtElement");
            foreach (XmlNode element in elements)
            {
                string typ = element.Attributes["LoetProtElementTyp"].Value;
                string name = element.Attributes["LoetProtElementName"].Value;
                string istwert = element.Attributes["LoetProtElementIstwert"].Value;
                string einheit = element.Attributes["LoetProtElementEinheit"]?.Value ?? string.Empty;
                string sollwert = element.Attributes["LoetProtElementSollwert"]?.Value ?? string.Empty;
                string toleranzPlus = element.Attributes["LoetProtElementToleranzPlus"]?.Value ?? string.Empty;
                string toleranzMinus = element.Attributes["LoetProtElementToleranzMinus"]?.Value ?? string.Empty;


                table.Rows.Add(line, equipmentName, library, programm, typ, name, istwert, einheit, sollwert, toleranzPlus, toleranzMinus, DateTime.Now.ToString());
            }

            // Mostrar la tabla en la consola
            foreach (DataRow row in table.Rows)
            {
                Console.WriteLine($"{row["Type"]}, {row["Name"]}, {row["CurrentValue"]}, {row["Unit"]}, {row["SetPoint"]}, {row["ToleranceUp"]}, {row["ToleranceDown"]}");
            }
            return table;
        }

        /// <summary>
        /// Reads coordinates from the specified log file path, and path coords
        /// </summary>
        /// <param name="pathLog"></param>
        /// <param name="pathCoords"></param>
        /// 

        private void InsertOrUpdateParametersSelective(DataTable dataTable)
        {
            using (SqlConnection connection = new SqlConnection(utils.connectionString))
            {
                connection.Open();

                foreach (DataRow row in dataTable.Rows)
                {
                    SqlCommand command = new SqlCommand("InsertOrUpdateParametersSelective", connection);
                    command.CommandType = CommandType.StoredProcedure;

                    // Agregar los parámetros al comando
                    command.Parameters.AddWithValue("@Line", row["Line"]);
                    command.Parameters.AddWithValue("@Equipment", row["Equipment"]);
                    command.Parameters.AddWithValue("@Library", row["Library"]);
                    command.Parameters.AddWithValue("@Program", row["Program"]);
                    command.Parameters.AddWithValue("@Type", row["Type"]);
                    command.Parameters.AddWithValue("@Name", row["Name"]);
                    command.Parameters.AddWithValue("@CurrentValue", row["CurrentValue"]);
                    command.Parameters.AddWithValue("@Unit", row["Unit"]);
                    command.Parameters.AddWithValue("@SetPoint", row["SetPoint"]);
                    command.Parameters.AddWithValue("@MaxValue", row["ToleranceUp"]);
                    command.Parameters.AddWithValue("@MinValue", row["ToleranceDown"]);
                    command.Parameters.AddWithValue("@LastUpdated", row["LastUpdated"]);

                    // Ejecutar el comando
                    command.ExecuteNonQuery();
                }

                connection.Close();
            }
        }

        static DataTable CompareDataTables(DataTable dataTable1, DataTable dataTable2)
        {
            DataTable differencesTable = new DataTable();

            // Agregar columnas al DataTable de diferencias
            differencesTable.Columns.Add("Line", typeof(string));
            differencesTable.Columns.Add("Equipment", typeof(string));
            differencesTable.Columns.Add("Library", typeof(string));
            differencesTable.Columns.Add("Program", typeof(string));
            differencesTable.Columns.Add("Type", typeof(string));
            differencesTable.Columns.Add("Name", typeof(string));
            differencesTable.Columns.Add("CurrentValue1", typeof(double));
            differencesTable.Columns.Add("SetPoint1", typeof(double));
            differencesTable.Columns.Add("SetPoint2", typeof(double));
            differencesTable.Columns.Add("MaxValue2", typeof(double));
            differencesTable.Columns.Add("MinValue2", typeof(double));
            differencesTable.Columns.Add("MessageError", typeof(string));
            // Iterar por cada fila en el primer DataTable
            foreach (DataRow row1 in dataTable1.Rows)
            {
                string line1 = row1["Line"].ToString();
                string equipment1 = row1["Equipment"].ToString();
                string library1 = row1["Library"].ToString();
                string program1 = row1["Program"].ToString();
                string type1 = row1["Type"].ToString();
                string name1 = row1["Name"].ToString();
                string currentValueString1 = row1["CurrentValue"].ToString();
                string setPointString1 = row1["SetPoint"].ToString();
                string maxValueString1 = row1["ToleranceUp"].ToString();
                string minValueString1 = row1["ToleranceDown"].ToString();

                if (type1 == "Vm21_Gm_ZeitPcbImModul")
                {

                }
                double currentValue1;
                if (!Double.TryParse(currentValueString1, out currentValue1))
                {
                    // No se pudo convertir el valor de CurrentValue a double
                    continue;
                }
                else if (currentValue1 == 0 && currentValueString1 != "0")
                {
                    continue;
                }

                double setPoint1;

                if (setPointString1 == "")
                {
                    setPoint1 = currentValue1;
                }

                else if (!Double.TryParse(setPointString1, out setPoint1))
                {
                    // No se pudo convertir el valor de SetPoint a double

                    continue;
                }
                else if (setPoint1 == 0)
                {
                    continue;
                }

                double maxValue1;
                if (maxValueString1 == "")
                {
                    maxValue1 = 0;
                }
                else if (!Double.TryParse(maxValueString1, out maxValue1))
                {
                    // No se pudo convertir el valor de MaxValue a double
                    continue;
                }

                double minValue1;
                if (minValueString1 == "")
                {
                    minValue1 = 0;
                }
                else if (!Double.TryParse(minValueString1, out minValue1))
                {
                    // No se pudo convertir el valor de MinValue a double
                    continue;
                }

                // Buscar la fila correspondiente en el segundo DataTable
                DataRow[] matchingRows = dataTable2.Select($"Line = '{line1}' AND Equipment = '{equipment1}' AND Program = '{program1}' AND Library = '{library1}' AND Type = '{type1}'");

                if (matchingRows.Length == 1)
                {
                    string setPointString2 = matchingRows[0]["SetPoint"].ToString();
                    // Comparar los valores
                    double setPoint2;

                    if (!Double.TryParse(setPointString2, out setPoint2))
                    {
                        // No se pudo convertir el valor de SetPoint a double
                        continue;
                    }

                    string maxValueString2 = matchingRows[0]["ToleranceUp"].ToString();
                    // Comparar los valores
                    double maxValue2;

                    if (!Double.TryParse(maxValueString2, out maxValue2))
                    {

                    }
                    else if (maxValueString2 == "NA")
                    {
                        maxValue2 = 0;
                    }
                    string minValueString2 = matchingRows[0]["ToleranceDown"].ToString();
                    // Comparar los valores
                    double minValue2;

                    if (!Double.TryParse(minValueString2, out minValue2))
                    {

                    }
                    else if (minValueString2 == "NA")
                    {
                        minValue2 = 0;
                    }

                    if (setPointString2 != "NA")
                    {
                        maxValue2 = maxValue2 + setPoint2;
                        minValue2 = setPoint2 - minValue2;
                        if ((currentValue1 > maxValue2 || currentValue1 < minValue2))
                        {
                            // Agregar la diferencia al DataTable de diferencias
                            DataRow differenceRow = differencesTable.NewRow();
                            differenceRow["Line"] = line1;
                            differenceRow["Equipment"] = equipment1;
                            differenceRow["Library"] = library1;
                            differenceRow["Program"] = program1;
                            differenceRow["Type"] = type1;
                            differenceRow["Name"] = name1;
                            differenceRow["CurrentValue1"] = currentValue1;
                            differenceRow["SetPoint1"] = setPoint1;
                            differenceRow["SetPoint2"] = setPoint2;
                            differenceRow["MinValue2"] = minValue2;
                            differenceRow["MaxValue2"] = maxValue2;
                            differenceRow["MessageError"] = $"Name: {name1}, Current Value: {currentValue1}, Set Point EPV: {setPoint2}, Tolerance Up: {maxValue2}, Tolerance Down: {minValue2}";
                            differencesTable.Rows.Add(differenceRow);
                        }
                        //else if (setPoint1 != setPoint2)
                        //{
                        //    // Agregar la diferencia al DataTable de diferencias
                        //    DataRow differenceRow = differencesTable.NewRow();
                        //    differenceRow["Line"] = line1;
                        //    differenceRow["Equipment"] = equipment1;
                        //    differenceRow["Library"] = library1;
                        //    differenceRow["Program"] = program1;
                        //    differenceRow["Type"] = type1;
                        //    differenceRow["Name"] = name1;
                        //    differenceRow["CurrentValue1"] = currentValue1;
                        //    differenceRow["SetPoint1"] = setPoint1;
                        //    differenceRow["SetPoint2"] = setPoint2;
                        //    differenceRow["MinValue2"] = minValue2;
                        //    differenceRow["MaxValue2"] = maxValue2;
                        //    differenceRow["MessageError"] = $"Name: {name1}, Set Point File {setPoint1} <> Set Point EPV: {setPoint2}";
                        //    differencesTable.Rows.Add(differenceRow);
                        //}
                    }
                }
            }

            return differencesTable;
        }

    
        private void readCoords(string pathLog, string pathCoords)
        {
            string library = "";
            string programName = "";
            string barcode = "";
            string equipmentName = "";
            string line = "";
            string side;
            string messageError = "";
            bool dailyRoute = false;
            string dailyRouteString = "";
            string[] files = { };
            int equipmentId = 0;
            int parserId = 0;
            bool coordenates = false;
            bool parameters = false;
            try
            {
                timer1.Stop();
                ClearInfo();
                var controller = ImageBehavior.GetAnimationController(ImgGif);
                files = Directory.GetFiles(pathLog);
                foreach (var file in files)
                {
                    var dt = utils.GetEquipmentByEquipmentName(lbltitle.Text);
                    if (dt.Rows.Count > 0)
                    {
                        if (dt.Rows[0]["DailyRoute"].ToString() == "True")
                        {
                            dailyRoute = true;
                            dailyRouteString = GetDailyRoute(pathLog);
                            files = Directory.GetFiles(dailyRouteString);
                        }
                        else
                        {
                            equipmentId = int.Parse(dt.Rows[0]["Equipment_ID"].ToString());
                            parserId = int.Parse(dt.Rows[0]["Parser_ID"].ToString());
                        }
                        parameters = bool.Parse(dt.Rows[0]["Parameters"].ToString());
                        coordenates = bool.Parse(dt.Rows[0]["Coordenates"].ToString());
                        if (parameters && !coordenates)
                        {
                            lblValidation.Text = "Parameters";
                        }
                        if (coordenates && !parameters)
                        {
                            lblValidation.Text = "Coordenates";
                        }
                        if (coordenates && parameters)
                        {
                            lblValidation.Text = "Parameters | Coordenates";
                        }
                    }
                    equipmentName = lbltitle.Text;
                    line = txtLine.Text;
                    DirectoryInfo info = new DirectoryInfo(file);
                    dt = new DataTable();
                    library = "";
                    int aux = 0;
                    string csvfilePath = info.FullName.EndsWith("\\") ? info.Parent.FullName.ToString() + info.Name : info.Parent.FullName.ToString() + "\\" + info.Name;
                    String URLString = csvfilePath;
                    var dr = dt.NewRow();
                    if (info.Extension.ToUpper() == ".XML")
                    {
                        var xml = new XmlDocument();
                        xml.Load(csvfilePath);

                        int auxlibprogbar = 0;
                        //convertimos xml a datatable
                        foreach (XmlNode item in xml.DocumentElement.ChildNodes)
                        {
                            if (item.Name == "LoetProtokoll")
                            {
                                foreach (XmlNode xmlNode in item.ChildNodes)
                                {
                                    var namenode = xmlNode.Attributes["LoetProtElementName"].Value;
                                    if (namenode == "Library" || namenode == "Librería")
                                    {
                                        library = xmlNode.Attributes["LoetProtElementIstwert"].Value;
                                        auxlibprogbar++;
                                    }
                                    if (namenode == "Program" || namenode == "Programa")
                                    {
                                        programName = xmlNode.Attributes["LoetProtElementIstwert"].Value;
                                        auxlibprogbar++;
                                    }
                                    if (namenode == "Serial board number" || namenode == "Número de serie de la placa")
                                    {
                                        barcode = xmlNode.Attributes["LoetProtElementIstwert"].Value;
                                        auxlibprogbar++;
                                    }
                                    if (auxlibprogbar == 3)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        dt.Rows.Add(dr);
                    }

                    var dtxml = XmltoDatatable(File.ReadAllText(info.FullName), cmbLine.Text, lbltitle.Text, programName, library);
                    DataTable dtdbparameters = null;
                    DataTable dtErrors = null;
                    int auxErrorFile = 0;
                    messageError = "";
                    if (parameters)
                    {
                        dtdbparameters = GetParametersSelective(cmbLine.Text, library, lbltitle.Text, programName);
                        dtErrors = CompareDataTables(dtxml, dtdbparameters);
                        if (dtdbparameters.Rows.Count == 0)
                        {
                            auxErrorFile++;
                            messageError += $"Dont found the Program {programName} in the system.";
                            results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + $"Dont found the Program {programName} in the system.", Date = DateTime.Now.ToString() });
                        }

                        else
                        {
                            if (parameters == true)
                            {

                                if (dtErrors.Rows.Count > 0)
                                {
                                    foreach (DataRow er in dtErrors.Rows)
                                    {
                                        messageError += $"{er["MessageError"]}\n";
                                        results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = $"{er["MessageError"]}\n", Date = DateTime.Now.ToString() });
                                        tblResults.CanUserResizeColumns = true;
                                        tblResults.ItemsSource = results;
                                        // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                        var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                                        // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                        tblResults.ScrollIntoView(lastItem);
                                        auxErrorFile++;
                                    }
                                }
                                else
                                {
                                    results.Add(new Result { SerialNumber = barcode, Status = "P", ResultMessage = "Pass Parameters", Date = DateTime.Now.ToString() });
                                    tblResults.UpdateLayout();
                                    tblResults.CanUserResizeColumns = true;
                                    tblResults.ItemsSource = results;
                                    utils.InsertHistoryParser(barcode, equipmentName, programName, "Pass", "Success");
                                    ProcessEvents();
                                }
                            }
                        }
                    }


                    string nameBackupFile1 = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                    DataTable dtdb = null;
                    bool filesexist;
                    File.Copy(csvfilePath, txtBackupFie.Text.EndsWith("\\") ? txtBackupFie.Text + nameBackupFile1 : txtBackupFie.Text + "\\" + nameBackupFile1);
                    File.Delete(csvfilePath);

                    if (coordenates == true)
                    {

                        pathCoords = pathCoords.EndsWith("\\") ? pathCoords : pathCoords + "\\";
                        if (!pathCoords.Contains($@"{library}\{programName}"))
                        {
                            pathCoords = pathCoords + $@"{library}\{programName}";
                        }
                        filesexist = Directory.Exists(pathCoords);


                        dtdb = utils.GetCoordsErsa1(cmbLine.Text, lbltitle.Text, programName);

                        if (!filesexist || dtdb.Rows.Count == 0)
                        {
                            if (!IsFormErrorOpen())
                            {
                                frmError = null;
                                frmError = new FormError();
                                frmError.Closed += (sender, args) => isFormErrorOpen = false;

                                //SmartFactory
                                var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                if (dtGeneralSettings.Rows.Count > 0)
                                {
                                    if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                                    {
                                        if (equipmentId == 0)
                                        {
                                            timer1.Stop();
                                            btnStart.Background = Brushes.Gold;
                                            btnPlay.Text = "Waiting";
                                            Status = "Waiting";
                                            lblServer.Document.Blocks.Clear();
                                            lblServer.AppendText("No machine id found");
                                            return;
                                        }
                                        else
                                        {
                                            if (!filesexist)
                                            {
                                                InsertGenericValidation(equipmentId, "ParameterValidation", $"Dont found {pathCoords}.");
                                                results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + $"Dont found {pathCoords}.", Date = DateTime.Now.ToString() });
                                            }
                                            else
                                            {
                                                InsertGenericValidation(equipmentId, "ParameterValidation", $"Dont found the Program {programName} in the system.");
                                                results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + $"Dont found the Program {programName} in the system.", Date = DateTime.Now.ToString() });
                                            }
                                        }
                                    }
                                }

                                tblResults.CanUserResizeColumns = true;
                                tblResults.ItemsSource = results;
                                // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                                // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                tblResults.ScrollIntoView(lastItem);
                                UpdateStatusParser(equipmentName, txtLine.Text, 0, "Warning");
                                utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", $"Dont found the Program {programName} in the system.");
                                frmError.Message = $"Dont found the Program {programName} in the system.";
                                frmError.Type = txtType.Text;
                                frmError.EquipmentName = lbltitle.Text;
                                frmError.equipmentId = equipmentId;
                                frmError.Line = txtLine.Text;
                                frmError.Show();
                                isFormErrorOpen = true;
                                break;
                            }
                            else
                            {
                                // Buscar el formulario FormSecundario en las ventanas abiertas
                                FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();
                                if (formSecundario != null)
                                {
                                    // Acceder al RichTextBox en FormSecundario y agregar el texto
                                    UpdateStatusParser(equipmentName, txtLine.Text, 0, "Warning");
                                    utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", $"Dont found the Program {programName} in the system.");
                                    formSecundario.Message = $"Dont found the Program {programName} in the system.";
                                    formSecundario.txtError.AppendText("\n" + $"Dont found the Program {programName} in the system.");
                                    formSecundario.Activate();
                                }
                                break;
                            }
                        }
                        else
                        {
                            var files1 = Directory.GetFiles(pathCoords);
                            if (pathCoords == "")
                            {
                                timer1.Stop();
                                btnStart.Background = Brushes.Gold;
                                btnPlay.Text = "Waiting";
                                Status = "Waiting";
                                MessageBox.Show("Dont found the path coords");
                            }


                            var dtParser = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                            if (dtParser.Rows.Count > 0)
                            {
                                foreach (DataRow p in dtParser.Rows)
                                {
                                    parserId = int.Parse(p["Parser_ID"].ToString());
                                    equipmentId = int.Parse(p["Equipment_ID"].ToString());
                                }
                            }

                            if (files1.Length > 0)
                            {
                                foreach (var file1 in files1)
                                {
                                    DirectoryInfo info1 = new DirectoryInfo(file1);

                                    if (info1.Name == "Satzdaten.csv")
                                    {

                                        var dtInfo = utils.sp_GetInfoParserByEquipmentName(lbltitle.Text);
                                        if (dtInfo.Rows.Count > 0)
                                        {
                                            decimal fpyValue = Convert.ToDecimal(dtInfo.Rows[0]["FPY"]);
                                            string fpyString = fpyValue.ToString("N2") + "%";
                                        }


                                        string csvfilePath1 = info.FullName.EndsWith("\\") ? info1.Parent.FullName.ToString() + info1.Name : info1.Parent.FullName.ToString() + "\\" + info1.Name;
                                        DataTable dtf = new DataTable();
                                        dtf = CSVToDataTableSelective(csvfilePath1).Tables[0];
                                        string nameBackupFile = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;



                                        var tableColumnNames = dtf.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                                        var schemaColumnNames = dtdb.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

                                        var unmatchedColumnNamesf =
                                            from col in tableColumnNames where !schemaColumnNames.Contains(col) select col;

                                        var unmatchedColumnNamesdb =
                                            from col in schemaColumnNames where !tableColumnNames.Contains(col) select col;
                                        foreach (var item in unmatchedColumnNamesf)
                                        {
                                            dtf.Columns.Remove(item);
                                        }

                                        foreach (var item in unmatchedColumnNamesdb)
                                        {
                                            dtdb.Columns.Remove(item);
                                        }
                                        // Obtener el orden de las columnas del primer DataTable
                                        var order = dtf.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

                                        // Ordenar las columnas del segundo DataTable según el orden del primer DataTable
                                        dtdb = new DataView(dtdb).ToTable(false, order.ToArray());

                                        if (dtdb.Columns.Count > 0 && dtf.Columns.Count > 0)
                                        {
                                            DataTable dt3 = dtf.Clone();
                                            dt3.Columns.Add("status");
                                            int auxrow = 0;
                                            int auxdif = 0;
                                            string error = "";
                                            var auxError = 0;


                                            int totalrowsdiff = dtf.Rows.Count - dtdb.Rows.Count;
                                            DataRow drdb;
                                            for (int i = 0; i < totalrowsdiff; i++)
                                            {
                                                drdb = dtdb.NewRow();
                                                dtdb.Rows.Add(drdb);
                                            }

                                            double tolerancia = 0;
                                            if (coordenates == true)
                                            {
                                                for (int f = 0; f < dtf.Rows.Count; f++)
                                                {

                                                    foreach (DataColumn coldb in dtdb.Columns)
                                                    {
                                                        if (coldb.ColumnName.ToString().StartsWith("Endposition X [mm]") ||
                                                            coldb.ColumnName.ToString().StartsWith("Endposition Y [mm]"))
                                                        {
                                                            tolerancia = 1.5;
                                                        }
                                                        else if (coldb.ColumnName.ToString().Contains("Endposition Z [mm]"))
                                                        {
                                                            tolerancia = 0.2;
                                                        }
                                                        else
                                                        {
                                                            tolerancia = 0;
                                                        }
                                                        if (dtf.Rows[f]["Set"].ToString().ToUpper().Trim() == dtdb.Rows[f]["Set"].ToString().ToUpper().Trim())
                                                        {
                                                            if (coldb.ColumnName == "Description" || coldb.ColumnName == "Description")
                                                            {
                                                                if (dtf.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim() == dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim())
                                                                {
                                                                    auxrow++;
                                                                }
                                                                else
                                                                {
                                                                    messageError += $"row: {f + 1}, col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtf.Rows[f][coldb.ColumnName]}\n";
                                                                    auxError++;
                                                                    Console.WriteLine($"row: {f + 1}, col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtf.Rows[f][coldb.ColumnName]}\n");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                bool result = IsFloatOrInt(dtf.Rows[f][coldb.ColumnName].ToString().Trim());
                                                                bool result1 = IsFloatOrInt(dtdb.Rows[f][coldb.ColumnName].ToString().Trim());
                                                                if (result && result1)
                                                                {

                                                                    if ((float.Parse(dtf.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) >= (float.Parse(dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) - tolerancia)) &&
                                                                        (float.Parse(dtf.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) <= (float.Parse(dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim()) + tolerancia)))
                                                                    {
                                                                        auxrow++;
                                                                    }
                                                                    else
                                                                    {
                                                                        messageError += $"row: {dtf.Rows[f]["Set"].ToString().ToUpper().Trim()}, col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtf.Rows[f][coldb.ColumnName]}\n";
                                                                        auxError++;
                                                                        Console.WriteLine($"row: {dtf.Rows[f]["Set"].ToString().ToUpper().Trim()}, col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtf.Rows[f][coldb.ColumnName]}\n");

                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    if (dtf.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim().Replace(" ", "").Replace("\t", "").Replace("\n", "") == dtdb.Rows[f][coldb.ColumnName].ToString().ToUpper().Trim().Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\0", ""))
                                                                    {
                                                                        auxrow++;
                                                                    }
                                                                    else
                                                                    {
                                                                        messageError += $"row: {dtf.Rows[f]["Set"].ToString().ToUpper().Trim()}, col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtf.Rows[f][coldb.ColumnName]}\n";
                                                                        auxError++;
                                                                        Console.WriteLine($"row: {dtf.Rows[f]["Set"].ToString().ToUpper().Trim()}, col: {coldb.ColumnName}, Set Point: {dtdb.Rows[f][coldb.ColumnName]}, Current Value: {dtf.Rows[f][coldb.ColumnName]}\n");
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    if (true)
                                                    {

                                                    }
                                                }
                                            }
                                            if (auxError > 0)
                                            {
                                                auxErrorFile++;

                                                results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail coordenates: " + messageError, Date = DateTime.Now.ToString() });
                                                tblResults.CanUserResizeColumns = true;
                                                tblResults.ItemsSource = results;

                                                // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                                var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                                                // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                                tblResults.ScrollIntoView(lastItem);

                                                UpdateStatusParser(equipmentName, txtLine.Text, 0, "Warning");
                                                utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", messageError);
                                                error += messageError;
                                                auxErrorFile++;
                                                ProcessEvents();
                                            }
                                            else
                                            {
                                                results.Add(new Result { SerialNumber = barcode, Status = "P", ResultMessage = "Pass Coordenates", Date = DateTime.Now.ToString() });
                                                tblResults.UpdateLayout();
                                                tblResults.CanUserResizeColumns = true;
                                                tblResults.ItemsSource = results;
                                                utils.InsertHistoryParser(barcode, equipmentName, programName, "Pass", "Success");
                                                ProcessEvents();
                                            }
                                        }
                                        else
                                        {
                                            timer1.Stop();
                                            btnStart.Background = Brushes.Gold;
                                            btnPlay.Text = "Waiting";
                                            Status = "Waiting";
                                            MessageBox.Show("No column match between database and log file");
                                            return;
                                        }

                                    }
                                }
                            }
                        }
                    }

                    if (auxErrorFile > 0)
                    { //SmartFactory
                        var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                        if (dtGeneralSettings.Rows.Count > 0)
                        {
                            if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                            {
                                if (equipmentId == 0)
                                {
                                    btnStart.Background = Brushes.Gold;
                                    btnPlay.Text = "Waiting";
                                    Status = "Waiting";
                                    MessageBox.Show("No machine id found");
                                    return;
                                }
                                else
                                {
                                    InsertGenericValidation(equipmentId, "ParameterValidation", "Coordinates out of range");
                                }
                            }
                        }
                        if (!IsFormErrorOpen())
                        {
                            frmError = new FormError();
                            frmError.Closed += (sender, args) => isFormErrorOpen = false;

                            UpdateStatusParser(equipmentName, line, 0, "Warning");
                            frmError.Message += messageError;
                            frmError.Type = txtType.Text;
                            frmError.EquipmentName = equipmentName;
                            frmError.equipmentId = equipmentId;
                            frmError.Line = line;
                            frmError.Show();
                            isFormErrorOpen = true;
                        }
                        else
                        {
                            // Buscar el formulario FormSecundario en las ventanas abiertas
                            FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                            if (formSecundario != null)
                            {
                                // Acceder al RichTextBox en FormSecundario y agregar el texto
                                formSecundario.Message = messageError;
                                formSecundario.txtError.AppendText("\n" + messageError);
                                UpdateStatusParser(equipmentName, line, 0, "Warning");
                            }
                            formSecundario.Activate();
                        }
                    }

                }
                tblResults.CanUserResizeColumns = true;
                tblResults.ItemsSource = results;
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("An error occurred while retrieving"))
                {
                    if (!IsFormErrorOpen())
                    {
                        var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                        if (dtGeneralSettings.Rows.Count > 0)
                        {
                            if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                            {

                                InsertGenericValidation(equipmentId, "ParameterValidation", "Coordinates out of range");

                            }

                            results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + e.Message, Date = DateTime.Now.ToString() });
                            // Obtén la referencia al último objeto en la colección de datos del DataGrid
                            var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                            // Llama al método ScrollIntoView para que el scroll se mueva al último item
                            tblResults.ScrollIntoView(lastItem);
                            ProcessEvents();
                            UpdateStatusParser(equipmentName, txtLine.Text, 0, "Warning");
                            utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", e.Message);
                            messageError = "";

                        }


                        frmError = new FormError();
                        frmError.Closed += (sender, args) => isFormErrorOpen = false;
                        UpdateStatusParser(equipmentName, line, 0, "Warning");
                        frmError.Message += e.Message;
                        frmError.Type = txtType.Text;
                        frmError.EquipmentName = lbltitle.Text;
                        frmError.equipmentId = equipmentId;
                        frmError.Line = line;
                        frmError.Show();
                        isFormErrorOpen = true;
                    }
                    else
                    {
                        // Buscar el formulario FormSecundario en las ventanas abiertas
                        FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                        if (formSecundario != null)
                        {
                            // Acceder al RichTextBox en FormSecundario y agregar el texto
                            formSecundario.Message = e.Message;
                            formSecundario.txtError.AppendText("\n" + e.Message);
                            UpdateStatusParser(equipmentName, line, 0, "Warning");
                        }
                        formSecundario.Activate();
                    }
                    Console.WriteLine(e.Message);
                }
                else
                {
                    results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + e.Message, Date = DateTime.Now.ToString() });
                    // Obtén la referencia al último objeto en la colección de datos del DataGrid
                    var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                    // Llama al método ScrollIntoView para que el scroll se mueva al último item
                    tblResults.ScrollIntoView(lastItem);
                    ProcessEvents();
                    utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", e.Message);
                }
            }
            timer1.Start();

        }

        private DataTable GetParametersSelective(string line, string Library, string equipmentName, string programName)
        {
            var dt = new DataTable();
            const string queryString = "exec [dbo].[sp_GetParametersSelectiveLib1] @Line, @Library, @EquipmentName, @ProgramName";
            using (var cnn = new SqlConnection(utils.connectionString))
            using (var cmd = new SqlCommand(queryString, cnn))
            using (var da = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.Add("@Line", SqlDbType.NVarChar).Value = line;
                cmd.Parameters.Add("@Library", SqlDbType.NVarChar).Value = Library;
                cmd.Parameters.Add("@EquipmentName", SqlDbType.NVarChar).Value = equipmentName;
                cmd.Parameters.Add("@ProgramName", SqlDbType.NVarChar).Value = programName;
                cmd.CommandTimeout = 30;
                da.Fill(dt);
            }
            return dt;
        }

        /// <summary>
        /// Retrieves the daily route path based on the root path.
        /// </summary>
        /// <param name="rutaRaiz">The root path.</param>
        /// <returns>The daily route path.</returns>
        public string GetDailyRoute(string rutaRaiz)
        {
            DateTime currentDate = DateTime.Now;
            string year = currentDate.Year.ToString();
            string month = currentDate.Month.ToString("00");
            string day = currentDate.Day.ToString("00");

            string path = Path.Combine(rutaRaiz, year, month, day);
            return path;
        }
        /// <summary>
        /// Retrieves the current REHM program name from a DataTable.
        /// </summary>
        /// <param name="dtf">The DataTable containing the program information.</param>
        /// <param name="lastRow">The index of the last row in the DataTable.</param>
        /// <returns>The current REHM program name.</returns>
        public string GetProgramCurrentREHM(DataTable dtf, int lastRow)
        {
            var programNameCurrent = "";
            string columnProgramName = "Machine Recipe";
            programNameCurrent = dtf.Rows[lastRow][columnProgramName].ToString();

            return programNameCurrent;
        }


        private static readonly HttpClient client = new HttpClient();
        public async Task<string> GetCurrentSetupByEquipmentAsync(string server, string machine)
        {
            var url = $"http://{server}/wstoolingmes/wstoolingMES.asmx?op=fnGetCurrentSetupByEquipment";
            var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
                                      <soap12:Body>
                                        <fnGetCurrentSetupByEquipment xmlns=""http://tempuri.org/"">
                                          <Machine>{machine}</Machine>
                                        </fnGetCurrentSetupByEquipment>
                                      </soap12:Body>
                                    </soap12:Envelope>";

            var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        public string ValidateProgramName(string responseXml, string expectedProgramName)
        {
            XDocument doc = XDocument.Parse(responseXml);
            XNamespace diffgr = "urn:schemas-microsoft-com:xml-diffgram-v1";
            XNamespace msdata = "urn:schemas-microsoft-com:xml-msdata";

            var programNames = doc.Descendants(diffgr + "diffgram")
                                  .Descendants("SetupEquipment")
                                  .Elements("ProgramName");

            string programName1 = "";
            foreach (var programName in programNames)
            {
                if (programName.Value == expectedProgramName)
                {
                    return "Ok";
                }
                programName1 = programName.ToString();
            }

            return programName1;
        }
        /// <summary>
        /// Reads coordinates from the specified log file path, and path coords
        /// </summary>
        /// <param name="pathLog"></param>
        /// <param name="pathCoords"></param>
        private async Task readREHMAsync(string pathLog, string pathCoords)
        {
            string library = "";
            string programName = "";
            string barcode = "";
            string equipmentName = "";
            string side;
            string messageError = "";
            bool dailyRoute = false;
            string dailyRouteString = "";
            string programNameCurrent = "";
            string line = "";
            int equipmentId = 0;
            string[] files = { };

            var outputFile = "";
            try
            {
                timer1.Stop();
                ClearInfo();

                var controller = ImageBehavior.GetAnimationController(ImgGif);

                var dtf = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                if (dtf.Rows.Count > 0)
                {
                    if (dtf.Rows[0]["DailyRoute"].ToString() == "True")
                    {
                        dailyRoute = true;
                        dailyRouteString = GetDailyRoute(pathLog);
                        files = Directory.GetFiles(dailyRouteString);
                    }
                    else
                    {
                        equipmentId = int.Parse(dtf.Rows[0]["Equipment_ID"].ToString());
                        files = Directory.GetFiles(pathLog);
                    }
                }
                foreach (var file in files)
                {
                    equipmentName = lbltitle.Text;
                    line = txtLine.Text;
                    DirectoryInfo info = new DirectoryInfo(file);
                    dtf = new DataTable();
                    library = "";
                    int aux = 0;
                    string nameBackupFile = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                    string csvfilePath = info.FullName.EndsWith("\\") ? info.Parent.FullName.ToString() + info.Name : info.Parent.FullName.ToString() + "\\" + info.Name;

                    String URLString = csvfilePath;
                    string fecha = DateTime.Now.ToString("yyyyMMdd");
                    if (info.Extension.ToUpper() == ".LOG" && info.Name.Contains(fecha))
                    {
                        string filePath = info.FullName;
                        dtf = CSVToDataTable(csvfilePath);
                        string campoBuscado = "DriveL1|Set"; // Supongamos que este es el nombre del campo que quieres comprobar
                        string columnProgramName = "Machine Recipe";
                        int numeroDeFila = -1; // Inicializamos el número de fila a -1 para indicar que no se ha encontrado el valor buscado
                        string outputFilePath = $"{info.Parent.FullName}\\output{fecha}.txt";
                        outputFile = outputFilePath;

                        if (!File.Exists(outputFilePath))
                        {
                            programNameCurrent = GetProgramCurrentREHM(dtf, dtf.Rows.Count - 1);

                            for (int i = dtf.Rows.Count - 1; i >= 0; i--)
                            {
                                DataRow fila = dtf.Rows[i];
                                if (fila[campoBuscado].ToString() != "")
                                {
                                    numeroDeFila = i;

                                    using (StreamWriter writer = new StreamWriter(outputFilePath))
                                    {
                                        writer.WriteLine(numeroDeFila);
                                    }
                                    break; // Salimos del bucle cuando encontramos el valor buscado en una fila
                                }
                            }
                        }
                        else
                        {
                            // Lee el contenido línea por línea y lo guarda en una lista
                            string[] lineas = File.ReadAllLines(outputFilePath);

                            programNameCurrent = GetProgramCurrentREHM(dtf, dtf.Rows.Count - 1);
                            foreach (string linea in lineas)
                            {
                                numeroDeFila = int.Parse(linea);
                                Console.WriteLine(linea);
                            }
                        }

                        var auxErrorFile = 0;
                        var auxError = 0;
                        string error = "";
                        for (int x = numeroDeFila; x < dtf.Rows.Count - 1; x++)
                        {
                            DataRow fila = dtf.Rows[x];
                            if (fila[campoBuscado].ToString() != "")
                            {
                                int numeroDeFilaEspecifico = numeroDeFila; // Supongamos que quieres obtener los datos a partir de la fila número 5
                                using (StreamWriter writer = new StreamWriter(outputFilePath))
                                {
                                    writer.WriteLine(x);
                                }
                                // Aquí puedes hacer lo que necesites con el número de fila encontrado
                                IEnumerable<DataRow> filas = dtf.AsEnumerable().Skip(numeroDeFilaEspecifico);
                                var dataTable = filas.CopyToDataTable();
                                auxError = 0;

                                //obtener parametros de la base de datos0
                                foreach (DataRow fila1 in filas)
                                {

                                    auxError = 0;
                                    int parserId = 0;
                                    var dtParser = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                                    //equipmentId = utils.GetEquipmentMesId(lbltitle.Text);
                                    if (dtParser.Rows.Count > 0)
                                    {
                                        foreach (DataRow p in dtParser.Rows)
                                        {
                                            parserId = int.Parse(p["Parser_ID"].ToString());
                                            equipmentId = int.Parse(p["Equipment_ID"].ToString());
                                            //SmartFactory
                                            var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                            if (dtGeneralSettings.Rows.Count > 0)
                                            {
                                                if (p["UseSmartFactory"].ToString() == "True" && p["ValidateSS"].ToString() == "True")
                                                {
                                                    if (equipmentId == 0)
                                                    {
                                                        timer1.Stop();
                                                        return;
                                                    }
                                                    else
                                                    {
                                                        var response = await GetCurrentSetupByEquipmentAsync("mxchim0meapps02", lbltitle.Text);
                                                        string SetupSheetPN = ValidateProgramName(response, programNameCurrent);

                                                        if (SetupSheetPN == "Ok")
                                                        {
                                                            Console.WriteLine("ProgramName is valid.");
                                                        }
                                                        else
                                                        {
                                                            Console.WriteLine("ProgramName is not valid.");
                                                            error = $"Program was not found in active sheet, Equipment recipe: {programNameCurrent}, Setup Sheet Program name: {SetupSheetPN}";
                                                            InsertGenericValidation(equipmentId, "ParameterValidation", $"Program was not found in active sheet, Equipment recipe: {programNameCurrent}, Setup Sheet Program name: {SetupSheetPN}");
                                                            results.Add(new Result { SerialNumber = "N/A", Status = "F", ResultMessage = "Fail: " + error, Date = DateTime.Now.ToString() });
                                                            auxErrorFile = 1;
                                                            if (!IsFormErrorOpen())
                                                            {
                                                                frmError = new FormError();
                                                                frmError.Closed += (sender, args) => isFormErrorOpen = false;

                                                                UpdateStatusParser(equipmentName, line, 0, "Warning");
                                                                frmError.Message += error;
                                                                frmError.Type = txtType.Text;
                                                                frmError.EquipmentName = equipmentName;
                                                                frmError.equipmentId = equipmentId;
                                                                frmError.Line = line;
                                                                frmError.Show();
                                                                isFormErrorOpen = true;
                                                            }
                                                            else
                                                            {
                                                                // Buscar el formulario FormSecundario en las ventanas abiertas
                                                                FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                                                                if (formSecundario != null)
                                                                {
                                                                    // Acceder al RichTextBox en FormSecundario y agregar el texto
                                                                    formSecundario.txtError.AppendText("\n" + error);
                                                                    UpdateStatusParser(equipmentName, line, 0, "Warning");
                                                                }
                                                                formSecundario.Activate();
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    DataTable dtdb = new DataTable();
                                    dtdb = GetParametersREHM(fila1[columnProgramName].ToString(), lbltitle.Text);
                                    programName = fila1[columnProgramName].ToString();

                                    if (fila1["Machine State"].ToString() == "DOWN" && fila1["CAMX Event Description"].ToString().Contains("Alarm"))
                                    {
                                        error += fila1["CAMX Event Description"].ToString();
                                        results.Add(new Result { SerialNumber = "N/A", Status = "F", ResultMessage = "Fail: " + error, Date = DateTime.Now.ToString() });

                                        //SmartFactory
                                        var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                        if (dtGeneralSettings.Rows.Count > 0)
                                        {
                                            if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                                            {
                                                if (equipmentId == 0)
                                                {
                                                    MessageBox.Show("No se encontro Id del equipo");
                                                    timer1.Stop();
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                    else if (fila1["DriveL1|Actual"].ToString() != "" && auxErrorFile == 0)
                                    {
                                        if (dtdb.Rows.Count > 0)
                                        {
                                            foreach (DataRow rdb in dtdb.Rows)
                                            {
                                                int flagError = 0;
                                                messageError = "";
                                                barcode = fila1["DateTimeStamp"].ToString();

                                                if (IsFloatOrInt(rdb["ConveyorSpeed"].ToString()) && IsFloatOrInt(rdb["ToleranceConveyorSpeed"].ToString()))
                                                {
                                                    var toleranceConveyorSpeedMin = float.Parse(rdb["ConveyorSpeed"].ToString()) - float.Parse(rdb["ToleranceConveyorSpeed"].ToString());
                                                    var toleranceConveyorSpeedMax = float.Parse(rdb["ConveyorSpeed"].ToString()) + float.Parse(rdb["ToleranceConveyorSpeed"].ToString());
                                                    if (float.Parse(fila1["DriveL1|Actual"].ToString()) < toleranceConveyorSpeedMin || float.Parse(fila1["DriveL1|Actual"].ToString()) > toleranceConveyorSpeedMax)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Conveyor Speed, Set Point: {rdb["ConveyorSpeed"]}, Current Value: {fila1["DriveL1|Actual"]}\n";
                                                        auxError++;
                                                        flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: Conveyor Speed:{rdb["ConveyorSpeed"]}, Tolerance Conveyor Speed:{rdb["ToleranceConveyorSpeed"]}\n";
                                                    auxError++;
                                                    flagError++;
                                                }
                                                //Temp ZONA 1
                                                if (IsFloatOrInt(rdb["TempZona1"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona1"].ToString()))
                                                {
                                                    var toleranceTempZona1Min = float.Parse(rdb["TempZona1"].ToString()) - float.Parse(rdb["ToleranceTempZona1"].ToString());
                                                    var toleranceTempZona1Max = float.Parse(rdb["TempZona1"].ToString()) + float.Parse(rdb["ToleranceTempZona1"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ01|Actual"].ToString()) < toleranceTempZona1Min || float.Parse(fila1["HtBottomZ01|Actual"].ToString()) > toleranceTempZona1Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona1, Set Point: {rdb["TempZona1"]}, Current Value: {fila1["HtBottomZ01|Actual"]}\n";
                                                        flagError++;
                                                        auxError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ01|Actual"].ToString()) < toleranceTempZona1Min || float.Parse(fila1["HtTopZ01|Actual"].ToString()) > toleranceTempZona1Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona1, Set Point: {rdb["TempZona1"]}, Current Value: {fila1["HtTopZ01|Actual"]}\n";
                                                        auxError++;
                                                        flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona1:{rdb["TempZona1"]}, Tolerance TempZona1: {rdb["ToleranceTempZona1"]}\n";
                                                    auxError++;
                                                    flagError++;
                                                }
                                                //Temp ZONA 2
                                                if (IsFloatOrInt(rdb["TempZona2"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona2"].ToString()))
                                                {
                                                    var toleranceTempZona2Min = float.Parse(rdb["TempZona2"].ToString()) - float.Parse(rdb["ToleranceTempZona2"].ToString());
                                                    var toleranceTempZona2Max = float.Parse(rdb["TempZona2"].ToString()) + float.Parse(rdb["ToleranceTempZona2"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ02|Actual"].ToString()) < toleranceTempZona2Min || float.Parse(fila1["HtBottomZ02|Actual"].ToString()) > toleranceTempZona2Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona2, Set Point: {rdb["TempZona2"]}, Current Value: {fila1["HtBottomZ02|Actual"]}\n";
                                                        auxError++;
                                                        flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ02|Actual"].ToString()) < toleranceTempZona2Min || float.Parse(fila1["HtTopZ02|Actual"].ToString()) > toleranceTempZona2Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona2, Set Point: {rdb["TempZona2"]}, Current Value: {fila1["HtTopZ02|Actual"]}\n";
                                                        auxError++;
                                                        flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona2:{rdb["TempZona2"]}, Tolerance TempZona2: {rdb["ToleranceTempZona2"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 3 // se ignora si el valor en archivo da 0, hasta que el proveedor corrija el error
                                                if (IsFloatOrInt(rdb["TempZona3"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona3"].ToString()))
                                                {
                                                    if (fila1["HtBottomZ03|Actual"].ToString() != "0")
                                                    {
                                                        var toleranceTempZona3Min = float.Parse(rdb["TempZona3"].ToString()) - float.Parse(rdb["ToleranceTempZona3"].ToString());
                                                        var toleranceTempZona3Max = float.Parse(rdb["TempZona3"].ToString()) + float.Parse(rdb["ToleranceTempZona3"].ToString());
                                                        if (float.Parse(fila1["HtBottomZ03|Actual"].ToString()) < toleranceTempZona3Min || float.Parse(fila1["HtBottomZ03|Actual"].ToString()) > toleranceTempZona3Max)
                                                        {
                                                            messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona3, Set Point: {rdb["TempZona3"]}, Current Value: {fila1["HtBottomZ03|Actual"]}\n";
                                                            auxError++; flagError++;
                                                        }
                                                    }
                                                    if (fila1["HtTopZ03|Actual"].ToString() != "0")
                                                    {
                                                        var toleranceTempZona3Min = float.Parse(rdb["TempZona3"].ToString()) - float.Parse(rdb["ToleranceTempZona3"].ToString());
                                                        var toleranceTempZona3Max = float.Parse(rdb["TempZona3"].ToString()) + float.Parse(rdb["ToleranceTempZona3"].ToString());
                                                        if (float.Parse(fila1["HtTopZ03|Actual"].ToString()) < toleranceTempZona3Min || float.Parse(fila1["HtTopZ03|Actual"].ToString()) > toleranceTempZona3Max)
                                                        {
                                                            messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona3, Set Point: {rdb["TempZona3"]}, Current Value: {fila1["HtTopZ03|Actual"]}\n";
                                                            auxError++; flagError++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona3:{rdb["TempZona3"]}, Tolerance TempZona3: {rdb["ToleranceTempZona3"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 4
                                                if (IsFloatOrInt(rdb["TempZona4"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona4"].ToString()))
                                                {
                                                    var toleranceTempZona4Min = float.Parse(rdb["TempZona4"].ToString()) - float.Parse(rdb["ToleranceTempZona4"].ToString());
                                                    var toleranceTempZona4Max = float.Parse(rdb["TempZona4"].ToString()) + float.Parse(rdb["ToleranceTempZona4"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ04|Actual"].ToString()) < toleranceTempZona4Min || float.Parse(fila1["HtBottomZ04|Actual"].ToString()) > toleranceTempZona4Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona4, Set Point: {rdb["TempZona4"]}, Current Value: {fila1["HtBottomZ04|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ04|Actual"].ToString()) < toleranceTempZona4Min || float.Parse(fila1["HtTopZ04|Actual"].ToString()) > toleranceTempZona4Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona4, Set Point: {rdb["TempZona4"]}, Current Value: {fila1["HtTopZ04|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona4:{rdb["TempZona4"]}, Tolerance TempZona4: {rdb["ToleranceTempZona4"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 5
                                                if (IsFloatOrInt(rdb["TempZona5"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona5"].ToString()))
                                                {
                                                    var toleranceTempZona5Min = float.Parse(rdb["TempZona5"].ToString()) - float.Parse(rdb["ToleranceTempZona5"].ToString());
                                                    var toleranceTempZona5Max = float.Parse(rdb["TempZona5"].ToString()) + float.Parse(rdb["ToleranceTempZona5"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ05|Actual"].ToString()) < toleranceTempZona5Min || float.Parse(fila1["HtBottomZ05|Actual"].ToString()) > toleranceTempZona5Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona5, Set Point: {rdb["TempZona5"]}, Current Value: {fila1["HtBottomZ05|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ05|Actual"].ToString()) < toleranceTempZona5Min || float.Parse(fila1["HtTopZ05|Actual"].ToString()) > toleranceTempZona5Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona5, Set Point: {rdb["TempZona5"]}, Current Value: {fila1["HtTopZ05|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona5:{rdb["TempZona5"]}, Tolerance TempZona5: {rdb["ToleranceTempZona5"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 6
                                                if (IsFloatOrInt(rdb["TempZona6"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona6"].ToString()))
                                                {
                                                    var toleranceTempZona6Min = float.Parse(rdb["TempZona6"].ToString()) - float.Parse(rdb["ToleranceTempZona6"].ToString());
                                                    var toleranceTempZona6Max = float.Parse(rdb["TempZona6"].ToString()) + float.Parse(rdb["ToleranceTempZona6"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ06|Actual"].ToString()) < toleranceTempZona6Min || float.Parse(fila1["HtBottomZ06|Actual"].ToString()) > toleranceTempZona6Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona6, Set Point: {rdb["TempZona6"]}, Current Value: {fila1["HtBottomZ06|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ06|Actual"].ToString()) < toleranceTempZona6Min || float.Parse(fila1["HtTopZ06|Actual"].ToString()) > toleranceTempZona6Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona6, Set Point: {rdb["TempZona6"]}, Current Value: {fila1["HtTopZ06|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona6:{rdb["TempZona6"]}, Tolerance TempZona6: {rdb["ToleranceTempZona6"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 7
                                                if (IsFloatOrInt(rdb["TempZona7"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona7"].ToString()))
                                                {
                                                    var toleranceTempZona7Min = float.Parse(rdb["TempZona7"].ToString()) - float.Parse(rdb["ToleranceTempZona7"].ToString());
                                                    var toleranceTempZona7Max = float.Parse(rdb["TempZona7"].ToString()) + float.Parse(rdb["ToleranceTempZona7"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ07|Actual"].ToString()) < toleranceTempZona7Min && float.Parse(fila1["HtBottomZ07|Actual"].ToString()) > toleranceTempZona7Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona7, Set Point: {rdb["TempZona7"]}, Current Value: {fila1["HtBottomZ07|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ07|Actual"].ToString()) < toleranceTempZona7Min && float.Parse(fila1["HtTopZ07|Actual"].ToString()) > toleranceTempZona7Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona7, Set Point: {rdb["TempZona7"]}, Current Value: {fila1["HtTopZ07|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona7:{rdb["TempZona7"]}, Tolerance TempZona7: {rdb["ToleranceTempZona7"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 8
                                                if (IsFloatOrInt(rdb["TempZona8"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona8"].ToString()))
                                                {
                                                    var toleranceTempZona8Min = float.Parse(rdb["TempZona8"].ToString()) - float.Parse(rdb["ToleranceTempZona8"].ToString());
                                                    var toleranceTempZona8Max = float.Parse(rdb["TempZona8"].ToString()) + float.Parse(rdb["ToleranceTempZona8"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ08|Actual"].ToString()) < toleranceTempZona8Min || float.Parse(fila1["HtBottomZ08|Actual"].ToString()) > toleranceTempZona8Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona8, Set Point: {rdb["TempZona8"]}, Current Value: {fila1["HtBottomZ08|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ08|Actual"].ToString()) < toleranceTempZona8Min || float.Parse(fila1["HtTopZ08|Actual"].ToString()) > toleranceTempZona8Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona8, Set Point: {rdb["TempZona8"]}, Current Value: {fila1["HtTopZ08|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona8:{rdb["TempZona8"]}, Tolerance TempZona8: {rdb["ToleranceTempZona8"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 9
                                                if (IsFloatOrInt(rdb["TempZona9"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona9"].ToString()))
                                                {
                                                    var toleranceTempZona9Min = float.Parse(rdb["TempZona9"].ToString()) - float.Parse(rdb["ToleranceTempZona9"].ToString());
                                                    var toleranceTempZona9Max = float.Parse(rdb["TempZona9"].ToString()) + float.Parse(rdb["ToleranceTempZona9"].ToString());
                                                    if (float.Parse(fila1["HtBottomZ09|Actual"].ToString()) < toleranceTempZona9Min || float.Parse(fila1["HtBottomZ09|Actual"].ToString()) > toleranceTempZona9Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona9, Set Point: {rdb["TempZona9"]}, Current Value: {fila1["HtBottomZ09|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopZ09|Actual"].ToString()) < toleranceTempZona9Min || float.Parse(fila1["HtTopZ09|Actual"].ToString()) > toleranceTempZona9Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona9, Set Point: {rdb["TempZona9"]}, Current Value: {fila1["HtTopZ09|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona9:{rdb["TempZona9"]}, Tolerance TempZona9: {rdb["ToleranceTempZona9"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 10
                                                if (IsFloatOrInt(rdb["TempZona10"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona10"].ToString()))
                                                {
                                                    var toleranceTempZona10Min = float.Parse(rdb["TempZona10"].ToString()) - float.Parse(rdb["ToleranceTempZona10"].ToString());
                                                    var toleranceTempZona10Max = float.Parse(rdb["TempZona10"].ToString()) + float.Parse(rdb["ToleranceTempZona10"].ToString());
                                                    if (float.Parse(fila1["HtBottomP1|Actual"].ToString()) < toleranceTempZona10Min || float.Parse(fila1["HtBottomP1|Actual"].ToString()) > toleranceTempZona10Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona10, Set Point: {rdb["TempZona10"]}, Current Value: {fila1["HtBottomP1|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopP1|Actual"].ToString()) < toleranceTempZona10Min || float.Parse(fila1["HtTopP1|Actual"].ToString()) > toleranceTempZona10Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona10, Set Point: {rdb["TempZona10"]}, Current Value: {fila1["HtTopP1|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona10:{rdb["TempZona10"]}, Tolerance TempZona10: {rdb["ToleranceTempZona10"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 11
                                                if (IsFloatOrInt(rdb["TempZona11"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona11"].ToString()))
                                                {
                                                    var toleranceTempZona11Min = float.Parse(rdb["TempZona11"].ToString()) - float.Parse(rdb["ToleranceTempZona11"].ToString());
                                                    var toleranceTempZona11Max = float.Parse(rdb["TempZona11"].ToString()) + float.Parse(rdb["ToleranceTempZona11"].ToString());
                                                    if (float.Parse(fila1["HtBottomP2|Actual"].ToString()) < toleranceTempZona11Min || float.Parse(fila1["HtBottomP2|Actual"].ToString()) > toleranceTempZona11Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona11, Set Point: {rdb["TempZona11"]}, Current Value: {fila1["HtBottomP2|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopP2|Actual"].ToString()) < toleranceTempZona11Min || float.Parse(fila1["HtTopP2|Actual"].ToString()) > toleranceTempZona11Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona11, Set Point: {rdb["TempZona11"]}, Current Value: {fila1["HtTopP2|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona11:{rdb["TempZona11"]}, Tolerance TempZona11: {rdb["ToleranceTempZona11"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 12
                                                if (IsFloatOrInt(rdb["TempZona12"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona12"].ToString()))
                                                {
                                                    var toleranceTempZona12Min = float.Parse(rdb["TempZona12"].ToString()) - float.Parse(rdb["ToleranceTempZona12"].ToString());
                                                    var toleranceTempZona12Max = float.Parse(rdb["TempZona12"].ToString()) + float.Parse(rdb["ToleranceTempZona12"].ToString());
                                                    if (float.Parse(fila1["HtBottomP3|Actual"].ToString()) < toleranceTempZona12Min || float.Parse(fila1["HtBottomP3|Actual"].ToString()) > toleranceTempZona12Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona12, Set Point: {rdb["TempZona12"]}, Current Value: {fila1["HtBottomP3|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopP3|Actual"].ToString()) < toleranceTempZona12Min || float.Parse(fila1["HtTopP3|Actual"].ToString()) > toleranceTempZona12Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona12, Set Point: {rdb["TempZona12"]}, Current Value: {fila1["HtTopP3|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona12:{rdb["TempZona12"]}, Tolerance TempZona12: {rdb["ToleranceTempZona12"]}\n";
                                                    auxError++; flagError++;
                                                }
                                                //Temp ZONA 13
                                                if (IsFloatOrInt(rdb["TempZona13"].ToString()) && IsFloatOrInt(rdb["ToleranceTempZona13"].ToString()))
                                                {
                                                    var toleranceTempZona13Min = float.Parse(rdb["TempZona13"].ToString()) - float.Parse(rdb["ToleranceTempZona13"].ToString());
                                                    var toleranceTempZona13Max = float.Parse(rdb["TempZona13"].ToString()) + float.Parse(rdb["ToleranceTempZona13"].ToString());
                                                    if (float.Parse(fila1["HtBottomVac|Actual"].ToString()) < toleranceTempZona13Min || float.Parse(fila1["HtBottomVac|Actual"].ToString()) > toleranceTempZona13Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona13, Set Point: {rdb["TempZona13"]}, Current Value: {fila1["HtBottomVac|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                    if (float.Parse(fila1["HtTopVac|Actual"].ToString()) < toleranceTempZona13Min || float.Parse(fila1["HtTopVac|Actual"].ToString()) > toleranceTempZona13Max)
                                                    {
                                                        messageError += $"row: {fila1["DateTimeStamp"]}, col: Temp Zona13, Set Point: {rdb["TempZona13"]}, Current Value: {fila1["HtTopVac|Actual"]}\n";
                                                        auxError++; flagError++;
                                                    }
                                                }
                                                else
                                                {
                                                    messageError += $"There is no data in one of these two fields in the system: TempZona13:{rdb["TempZona13"]}, Tolerance TempZona13: {rdb["ToleranceTempZona13"]}\n";
                                                    auxError++; flagError++;
                                                }

                                                if (rdb["VacuumMode"].ToString() == "True")
                                                {

                                                    
                                                    if (float.Parse(fila1["VacuumPressure|Actual"].ToString()) == 9999)
                                                    {
                                                        messageError += $"El programa actual {programName} debe tener VACIO y esta apagado en OVEN. Contactar al SME o Ingeniero del proceso\n";
                                                        auxError++; flagError++;
                                                    }
                                                    else
                                                    {
                                                        if (IsFloatOrInt(rdb["VacuumTargetPressure1"].ToString()) && IsFloatOrInt(rdb["ToleranceTargetPressureMin1"].ToString()))
                                                        {
                                                            var toleranceVacuumPressure1Min = float.Parse(rdb["VacuumTargetPressure1"].ToString()) - float.Parse(rdb["ToleranceTargetPressureMin1"].ToString());

                                                            if (float.Parse(fila1["VacuumPressure|Actual"].ToString()) < toleranceVacuumPressure1Min || float.Parse(fila1["VacuumPressure|Actual"].ToString()) > float.Parse(rdb["VacuumTargetPressure1"].ToString()))
                                                            {
                                                                messageError += $"row: {fila1["DateTimeStamp"]}, col: Vacuum Pressure 1, Set Point: {rdb["VacuumTargetPressure1"]}, Current Value: {fila1["VacuumPressure|Actual"]}\n";
                                                                auxError++; flagError++;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            messageError += $"There is no data in one of these two fields in the system: Vacuum Pressure 1:{rdb["VacuumTargetPressure1"]}, Current Value: {fila1["VacuumPressure|Actual"]}\n";
                                                            auxError++; flagError++;
                                                        }
                                                        if (IsFloatOrInt(rdb["ControlValve1"].ToString()) && IsFloatOrInt(rdb["ToleranceControlValve1"].ToString()))
                                                        {
                                                            var toleranceControlValve1Min = float.Parse(rdb["ControlValve1"].ToString()) - float.Parse(rdb["ToleranceControlValve1"].ToString());
                                                            var toleranceControlValve1Max = float.Parse(rdb["ControlValve1"].ToString()) + float.Parse(rdb["ToleranceControlValve1"].ToString());
                                                            if (float.Parse(fila1["VacStep0ValveSet"].ToString()) < toleranceControlValve1Min || float.Parse(fila1["VacStep0ValveSet"].ToString()) > toleranceControlValve1Max)
                                                            {
                                                                messageError += $"row: {fila1["DateTimeStamp"]}, col: ControlValve1, Set Point: {rdb["ControlValve1"]}, Current Value: {fila1["VacStep0ValveSet"]}\n";
                                                                auxError++; flagError++;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            messageError += $"There is no data in one of these two fields in the system: ControlValve1:{rdb["ControlValve1"]}, Current Value: {fila1["VacStep0ValveSet"]}\n";
                                                            auxError++; flagError++;
                                                        }
                                                        if (IsFloatOrInt(rdb["VacuumSetTime1"].ToString()) && IsFloatOrInt(rdb["ToleranceVacuumSetTime1"].ToString()))
                                                        {
                                                            var toleranceVacuumSetTime1Min = float.Parse(rdb["VacuumSetTime1"].ToString()) - float.Parse(rdb["ToleranceVacuumSetTime1"].ToString());
                                                            var toleranceVacuumSetTime1Max = float.Parse(rdb["VacuumSetTime1"].ToString()) + float.Parse(rdb["ToleranceVacuumSetTime1"].ToString());
                                                            if (float.Parse(fila1["VacStep0TimeSet"].ToString()) < toleranceVacuumSetTime1Min || float.Parse(fila1["VacStep0TimeSet"].ToString()) > toleranceVacuumSetTime1Max)
                                                            {
                                                                messageError += $"row: {fila1["DateTimeStamp"]}, col: VacuumSetTime1, Set Point: {rdb["VacuumSetTime1"]}, Current Value: {fila1["VacStep0TimeSet"]}\n";
                                                                auxError++; flagError++;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            messageError += $"There is no data in one of these two fields in the system: VacuumSetTime1:{rdb["VacuumSetTime1"]}, Current Value: {fila1["VacStep0TimeSet"]}\n";
                                                            auxError++; flagError++;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (float.Parse(fila1["VacuumPressure|Actual"].ToString()) != 9999)
                                                    {
                                                        messageError += $"El programa actual {programName} no lleva VACIO y esta habilitado en el horno, favor de contactar a Ingenieria de Manufactura\n";
                                                        auxError++; flagError++;
                                                    }
                                                }

                                                if (auxError > 0)
                                                {
                                                    //SmartFactory
                                                    var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                                    if (dtGeneralSettings.Rows.Count > 0)
                                                    {
                                                        if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                                                        {
                                                            if (equipmentId == 0)
                                                            {
                                                                MessageBox.Show("No se encontro Id del equipo");
                                                                timer1.Stop();
                                                                return;
                                                            }
                                                            else
                                                            {
                                                                InsertGenericValidation(equipmentId, "ParameterValidation", "Limits out range");
                                                            }
                                                        }
                                                    }
                                                    results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });
                                                    tblResults.CanUserResizeColumns = true;
                                                    tblResults.ItemsSource = results;
                                                    // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                                    var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                                                    // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                                    tblResults.ScrollIntoView(lastItem);
                                                    ProcessEvents();
                                                    utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", messageError);
                                                    error += messageError;
                                                    messageError = "";
                                                    auxErrorFile++;
                                                }
                                                if (flagError == 0)
                                                {
                                                    results.Add(new Result { SerialNumber = barcode, Status = "P", ResultMessage = "Pass", Date = DateTime.Now.ToString() });
                                                    tblResults.CanUserResizeColumns = true;
                                                    tblResults.ItemsSource = results;
                                                    // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                                    var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                                                    // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                                    tblResults.ScrollIntoView(lastItem);
                                                    ProcessEvents();
                                                    utils.InsertHistoryParser(barcode, equipmentName, programName, "Pass", "Success");
                                                }
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });
                                            tblResults.CanUserResizeColumns = true;
                                            tblResults.ItemsSource = results;
                                            // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                            var lastItem = tblResults.Items[tblResults.Items.Count - 1];
                                            error += $"El programa {programName} no esta cargado al EPV System, Contactar al SME del proceso.";
                                            InsertGenericValidation(equipmentId, "ParameterValidation", "Programa desconocido en EPV System");
                                            ProcessEvents();
                                            auxErrorFile++;
                                            break;
                                        }
                                    }
                                }
                                break;
                            }
                        }
                        using (StreamWriter writer = new StreamWriter(outputFilePath))
                        {
                            writer.WriteLine(dtf.Rows.Count);
                        }
                        numeroDeFila = dtf.Rows.Count;
                        if (auxErrorFile > 0)
                        {
                            if (!IsFormErrorOpen())
                            {
                                frmError = new FormError();
                                frmError.Closed += (sender, args) => isFormErrorOpen = false;
                                auxError = 0;
                                UpdateStatusParser(equipmentName, line, 0, "Warning");
                                frmError.Message += error;
                                frmError.Type = txtType.Text;
                                frmError.EquipmentName = equipmentName;
                                frmError.equipmentId = equipmentId;
                                frmError.Line = line;
                                frmError.Show();
                                isFormErrorOpen = true;
                            }
                            else
                            {
                                // Buscar el formulario FormSecundario en las ventanas abiertas
                                FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                                if (formSecundario != null)
                                {
                                    // Acceder al RichTextBox en FormSecundario y agregar el texto
                                    formSecundario.txtError.AppendText("\n" + error);
                                    UpdateStatusParser(equipmentName, line, 0, "Warning");
                                }
                                formSecundario.Activate();
                            }
                        }
                    }

                }
                //  controller.Play();
                tblResults.CanUserResizeColumns = true;
                tblResults.ItemsSource = results;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("DriveL1"))
                {
                    if (File.Exists(outputFile))
                    {
                        File.Delete(outputFile);
                    }
                }
                else
                {
                    if (!IsFormErrorOpen())
                    {
                        isFormErrorOpen = true;
                        frmError = new FormError();
                        frmError.Message += e.Message;
                        frmError.Closed += (sender, args) => isFormErrorOpen = false;
                        InsertGenericValidation(equipmentId, "ParameterValidation", e.Message);
                        UpdateStatusParser(equipmentName, line, 0, "Warning");
                        frmError.Type = txtType.Text;
                        frmError.EquipmentName = equipmentName;
                        frmError.equipmentId = equipmentId;
                        frmError.Line = line;
                        frmError.Show();

                    }
                    else
                    {
                        InsertGenericValidation(equipmentId, "ParameterValidation", e.Message);
                        UpdateStatusParser(equipmentName, line, 0, "Warning");
                        // Buscar el formulario FormSecundario en las ventanas abiertas
                        FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                        if (formSecundario != null)
                        {
                            // Acceder al RichTextBox en FormSecundario y agregar el texto
                            formSecundario.txtError.AppendText("\n" + e.Message + "\n");
                        }
                        formSecundario.Activate();
                    }
                }

                Console.WriteLine(e.Message);
            }

            timer1.Start();
        }// Dentro de un método o evento de tu aplicación WPF        



        private async Task readKY(string pathLog, string pathCoords)
        {
            string library = "";
            string programName = "";
            string barcode = "";
            string equipmentName = "";
            string side;
            string messageError = "";
            bool dailyRoute = false;
            string dailyRouteString = "";
            string programNameCurrent = "";
            string line = "";
            int equipmentId = 0;
            string[] files = { };

            var outputFile = "";
            try
            {
                timer1.Stop();
                ClearInfo();

                var controller = ImageBehavior.GetAnimationController(ImgGif);

                var dtf = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                if (dtf.Rows.Count > 0)
                {
                    if (dtf.Rows[0]["DailyRoute"].ToString() == "True")
                    {
                        dailyRoute = true;
                        dailyRouteString = GetDailyRoute(pathLog);
                        files = Directory.GetFiles(dailyRouteString);
                    }
                    else
                    {
                        equipmentId = int.Parse(dtf.Rows[0]["Equipment_ID"].ToString());
                        files = Directory.GetFiles(pathLog);
                    }
                }
                foreach (var file in files)
                {
                    equipmentName = lbltitle.Text;
                    line = txtLine.Text;
                    DirectoryInfo info = new DirectoryInfo(file);
                    dtf = new DataTable();
                    library = "";
                    int aux = 0;
                    string nameBackupFile = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                    string csvfilePath = info.FullName.EndsWith("\\") ? info.Parent.FullName.ToString() + info.Name : info.Parent.FullName.ToString() + "\\" + info.Name;



                    if (info.Extension.ToUpper().EndsWith(".LOG"))
                    {

                        // Utilizar FileStream con FileShare.ReadWrite para leer el archivo aunque esté en uso por otro proceso
                        using (FileStream fileStream = new FileStream(csvfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader reader = new StreamReader(fileStream))
                        {
                            string line1;
                            int lockedValue = 0;
                            string programModifiedValue = string.Empty;
                            int equipmentIdValue = 0;

                            // Leer cada línea del archivo
                            while ((line1 = reader.ReadLine()) != null)
                            {
                                // Verificar si la línea contiene los datos que necesitamos
                                if (line1.StartsWith("Locked ="))
                                {
                                    lockedValue = int.Parse(line1.Split('=')[1].Trim());
                                }
                                else if (line1.StartsWith("Program Modified ="))
                                {
                                    programModifiedValue = line1.Split('=')[1].Trim();
                                }
                                else if (line1.StartsWith("EquipmentID ="))
                                {
                                    equipmentIdValue = int.Parse(line1.Split('=')[1].Trim());
                                }
                            }


                            if (lockedValue == 1)
                            {
                                var dtParser = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                                equipmentId = 0;
                                int parserId = 0;
                                if (dtParser != null)
                                {
                                    foreach (DataRow p in dtParser.Rows)
                                    {
                                        parserId = int.Parse(p["Parser_ID"].ToString());
                                        equipmentId = int.Parse(p["Equipment_ID"].ToString());
                                    }
                                }
                                //SmartFactory
                                var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                if (dtGeneralSettings.Rows.Count > 0)
                                {
                                    if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                                    {
                                        if (equipmentId == 0)
                                        {
                                            MessageBox.Show("No se encontro Id del equipo");
                                            timer1.Stop();
                                            return;
                                        }
                                        else
                                        {
                                            InsertGenericValidation(equipmentId, "ParameterValidation", "Limits out range");
                                        }
                                    }
                                }
                                messageError = $@"Program modified:
{programModifiedValue}
Please contact SPI Process SME or Connectivity Area ";

                                results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });

                                // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                tblResults.CanUserResizeColumns = true;
                                tblResults.ItemsSource = results;
                                ProcessEvents();
                                utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", messageError);




                                if (!IsFormErrorOpen())
                                {
                                    isFormErrorOpen = true;
                                    frmError = new FormError();
                                    frmError.Message += messageError;
                                    frmError.Closed += (sender, args) => isFormErrorOpen = false;
                                    InsertGenericValidation(equipmentId, "ParameterValidation", messageError);
                                    UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                                    frmError.Type = txtType.Text;
                                    frmError.EquipmentName = equipmentName;
                                    frmError.equipmentId = equipmentId;
                                    frmError.Line = cmbLine.Text;
                                    frmError.Show();
                                }
                                else
                                {
                                    InsertGenericValidation(equipmentId, "ParameterValidation", messageError);
                                    UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                                    // Buscar el formulario FormSecundario en las ventanas abiertas
                                    FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                                    if (formSecundario != null)
                                    {
                                        // Acceder al RichTextBox en FormSecundario y agregar el texto
                                        formSecundario.txtError.AppendText("\n" + messageError + "\n");
                                    }
                                    formSecundario.Activate();
                                }

                            }
                            else
                            {
                                results.Add(new Result { SerialNumber = barcode, Status = "P", ResultMessage = "Success", Date = DateTime.Now.ToString() });

                                // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                tblResults.CanUserResizeColumns = true;
                                tblResults.ItemsSource = results;
                                ProcessEvents();
                                utils.InsertHistoryParser(barcode, equipmentName, programName, "Pass", "Success");


                            }
                        }


                    }

                }
                //  controller.Play();
                tblResults.CanUserResizeColumns = true;
                tblResults.ItemsSource = results;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("DriveL1"))
                {
                    if (File.Exists(outputFile))
                    {
                        File.Delete(outputFile);
                    }
                }
                else
                {
                    if (!IsFormErrorOpen())
                    {
                        isFormErrorOpen = true;
                        frmError = new FormError();
                        frmError.Message += e.Message;
                        frmError.Closed += (sender, args) => isFormErrorOpen = false;
                        InsertGenericValidation(equipmentId, "ParameterValidation", e.Message);
                        UpdateStatusParser(equipmentName, line, 0, "Warning");
                        frmError.Type = txtType.Text;
                        frmError.EquipmentName = equipmentName;
                        frmError.equipmentId = equipmentId;
                        frmError.Line = line;
                        frmError.Show();
                    }
                    else
                    {
                        InsertGenericValidation(equipmentId, "ParameterValidation", e.Message);
                        UpdateStatusParser(equipmentName, line, 0, "Warning");
                        // Buscar el formulario FormSecundario en las ventanas abiertas
                        FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();
                        if (formSecundario != null)
                        {
                            // Acceder al RichTextBox en FormSecundario y agregar el texto
                            formSecundario.txtError.AppendText("\n" + e.Message + "\n");
                        }
                        formSecundario.Activate();
                    }
                }

                Console.WriteLine(e.Message);
            }

            timer1.Start();
        }// Dentro de un método o evento de tu aplicación WPF        



        /// <summary>
        /// Process Events
        /// </summary>
        private void ProcessEvents()
        {
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        /// <summary>
        /// Checks if the FormError window is open in the current application.
        /// </summary>
        /// <returns>True if the FormError window is open; otherwise, false.</returns>
        private bool IsFormErrorOpen()
        {
            foreach (System.Windows.Window window in Application.Current.Windows)
            {
                if (window is FormError)
                {
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Clear Info
        /// </summary>
        private void ClearInfo()
        {
            lblEquipmentName.Text = "Equipment Name";
            lblUsername.Text = "";
        }


        /// <summary>
        /// Reads parameters from the specified log file path.
        /// </summary>
        /// <param name="pathLog">The path to the log file.</param>
        private void readLogOven(string pathLog)
        {
            string barcode = "";
            string programName = "";
            string equipmentName = "";
            string line = "";
            string side;
            string messageError = "";
            try
            {
                ClearInfo();
                var files = Directory.GetFiles(pathLog);
                var controller = ImageBehavior.GetAnimationController(ImgGif);
                int equipmentId = 0;
                int parserId = 0;
                int auxErrorFile = 0;
                string error = "";
                foreach (var file in files)
                {
                    equipmentName = lbltitle.Text;
                    line = txtLine.Text;
                    DirectoryInfo info = new DirectoryInfo(file);
                    DataTable dt = new DataTable();
                    barcode = "";
                    int aux = 0;
                    string csvfilePath = info.FullName.EndsWith("\\") ? info.Parent.FullName.ToString() + info.Name : info.Parent.FullName.ToString() + "\\" + info.Name;
                    String URLString = csvfilePath;
                    var dr = dt.NewRow();
                    if (info.Extension.ToUpper() == ".XML")
                    {
                        var xml = new XmlDocument();
                        xml.Load(csvfilePath);

                        //convertimos xml a datatable
                        foreach (XmlNode item in xml.DocumentElement.ChildNodes)
                        {
                            if (item.Name == "processingParameters")
                            {
                                foreach (XmlNode xmlNode in item.ChildNodes)
                                {
                                    var namenode = xmlNode.Attributes["name"].Value;
                                    if (namenode == "Serial board number" || namenode == "Número de serie de la placa")
                                        barcode = xmlNode.Attributes["value"].Value;
                                    if (namenode == "Program name" || namenode == "Nom. Programa")
                                        programName = xmlNode.Attributes["value"].Value;
                                }
                            }
                            if (item.Name == "measuring")
                                foreach (XmlNode xmlNode in item.ChildNodes)
                                {
                                    var ValidationName = xmlNode.Attributes["name"].Value;
                                    foreach (XmlNode values in xmlNode.ChildNodes)
                                    {
                                        if (values.Name == "sample")
                                        {
                                            foreach (DataColumn col in dt.Columns)
                                                if (col.ColumnName == ValidationName)
                                                    aux++;
                                            if (aux == 0)
                                            {
                                                var value = values.Attributes["value"].Value;
                                                dt.Columns.Add(ValidationName);
                                                dr[ValidationName] = value;
                                            }
                                            aux = 0;
                                        }
                                    }
                                }
                        }
                        dt.Rows.Add(dr);

                    }
                    error = "";
                    var auxError = 0;
                    auxErrorFile = 0;
                    var dtParser = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                    equipmentId = 0;
                    parserId = 0;
                    if (dtParser != null)
                    {
                        foreach (DataRow p in dtParser.Rows)
                        {
                            parserId = int.Parse(p["Parser_ID"].ToString());
                            equipmentId = int.Parse(p["Equipment_ID"].ToString());
                        }
                    }

                    DataTable dtParameters = GetParametersOven(programName, lbltitle.Text);
                    if (dtParameters.Rows.Count > 0)
                    {
                        string nameBackupFile = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                        File.Copy(file, txtBackupFie.Text.EndsWith("\\") ? txtBackupFie.Text + nameBackupFile : txtBackupFie.Text + "\\" + nameBackupFile);
                        File.Delete(file);
                        foreach (DataRow row in dt.Rows)//Log
                        {
                            auxError = 0;
                            foreach (DataRow parameter in dtParameters.Rows)//wsb
                            {
                                var centersupportf = 0.0;
                                var vacuumtargetpressuref = 0.0;
                                var vacuumsettimef = 0.0;
                                var controlevalvef = 0.0;
                                var holdtimef = 0.0;
                                var vacuumUpperTempf = 0.0;
                                var vacuumLowerTempf = 0.0;

                                if (lbltitle.Text != "O-ERSA1026-01")
                                    centersupportf = row.Table.Columns.Contains("Center support  | Position Y  (mm)")
                                                    ? float.Parse(row["Center support  | Position Y  (mm)"].ToString())
                                                    : row.Table.Columns.Contains("Soporte central  | Posición Y  (mm)")
                                                        ? float.Parse(row["Soporte central  | Posición Y  (mm)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;


                                var conveorspeedf = row.Table.Columns.Contains("Conveyor  | speed  (cm/min)")
                                                    ? float.Parse(row["Conveyor  | speed  (cm/min)"].ToString())
                                                    : row.Table.Columns.Contains("Transportador  | velocidad  (cm/min)")
                                                        ? float.Parse(row["Transportador  | velocidad  (cm/min)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;


                                var tempzona1f = row.Table.Columns.Contains("Heating  | Upper heating 1 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 1 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Heating  | Heating top 1 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 1 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 1 | Temperatura  (°C)")
                                                    ? float.Parse(row["Calentamiento  | Calentamiento superior 1 | Temperatura  (°C)"].ToString())
                                                    : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona2f = row.Table.Columns.Contains("Heating  | Upper heating 2 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 2 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Heating  | Heating top 2 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 2 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 2 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 2 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona3f = row.Table.Columns.Contains("Heating  | Upper heating 3 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 3 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 3 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 3 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 3 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 3 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona4f = row.Table.Columns.Contains("Heating  | Upper heating 4 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 4 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 4 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 4 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 4 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 4 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona5f = row.Table.Columns.Contains("Heating  | Upper heating 5 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 5 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 5 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 5 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 5 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 5 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona6f = row.Table.Columns.Contains("Heating  | Upper heating 6 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 6 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 6 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 6 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 6 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 6 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona7f = row.Table.Columns.Contains("Heating  | Upper heating 7 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 7 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 7 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 7 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 7 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 7 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona8f = row.Table.Columns.Contains("Heating  | Upper heating 8 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 8 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 8 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 8 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 8 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 8 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona9f = row.Table.Columns.Contains("Heating  | Upper heating 9 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 9 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 9 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 9 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 9 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 9 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona10f = row.Table.Columns.Contains("Heating  | Upper heating 10 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 10 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 10 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 10 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 10 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 10 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                var tempzona11f = row.Table.Columns.Contains("Heating  | Upper heating 11 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating 11 | Temperature  (°C)"].ToString())
                                                       : row.Table.Columns.Contains("Heating  | Heating top 11 | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Heating top 11 | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior 11 | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior 11 | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                // var tempzona12f = float.Parse(row["Heating  | Upper heating 12 | Temperature  (°C)"].ToString());

                                var vacuumactive = parameter["VacuumActive"].ToString();
                                if (vacuumactive == "True")
                                {
                                    vacuumtargetpressuref = row.Table.Columns.Contains("Vacuum  | Pressure evacuation  (mbar)")
                                                            ? float.Parse(row["Vacuum  | Pressure evacuation  (mbar)"].ToString().Replace(',', '.'))
                                                            : row.Table.Columns.Contains("Vacuum  | Evacuation 1 | Target pressure  (mbar)")
                                                                ? float.Parse(row["Vacuum  | Evacuation 1 | Target pressure  (mbar)"].ToString().Replace(',', '.'))
                                                            : row.Table.Columns.Contains("Aspiración  | Purgar presión  (mbar)")
                                                                ? float.Parse(row["Aspiración  | Purgar presión  (mbar)"].ToString().Replace(',', '.'))
                                                                : 0.0f; // Valor por defecto si ninguno de los campos existe


                                    vacuumsettimef = row.Table.Columns.Contains("Vacuum  | Process time  (s)")
                                                    ? float.Parse(row["Vacuum  | Process time  (s)"].ToString())
                                                    : row.Table.Columns.Contains("Vacuum  | Evacuation 1 | Holding time  (s)")
                                                    ? float.Parse(row["Vacuum  | Evacuation 1 | Holding time  (s)"].ToString().Replace(',', '.'))
                                                    : row.Table.Columns.Contains("Aspiración  | Tiempo de proceso  (s)")
                                                        ? float.Parse(row["Aspiración  | Tiempo de proceso  (s)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;;
                                    controlevalvef = row.Table.Columns.Contains("Vacuum  | Power value ventilation  (%)")
                                                    ? float.Parse(row["Vacuum  | Power value ventilation  (%)"].ToString())
                                                     : row.Table.Columns.Contains("Vacuum  | Ventilation 1 | Power value ventilation  (%)")
                                                        ? float.Parse(row["Vacuum  | Ventilation 1 | Power value ventilation  (%)"].ToString())
                                                    : row.Table.Columns.Contains("Aspiración  | Valor de potencia de ventilación  (%)")
                                                        ? float.Parse(row["Aspiración  | Valor de potencia de ventilación  (%)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;
                                    holdtimef = row.Table.Columns.Contains("Vacuum  | Process time  (s)")
                                                    ? float.Parse(row["Vacuum  | Process time  (s)"].ToString())
                                                     : row.Table.Columns.Contains("Vacuum  | Evacuation 1 | Holding time  (s)")
                                                        ? float.Parse(row["Vacuum  | Evacuation 1 | Holding time  (s)"].ToString())
                                                    : row.Table.Columns.Contains("Aspiración  | Tiempo de proceso  (s)")
                                                        ? float.Parse(row["Aspiración  | Tiempo de proceso  (s)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;
                                    vacuumUpperTempf = row.Table.Columns.Contains("Heating  | Upper heating VAC | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Upper heating VAC | Temperature  (°C)"].ToString())
                                                     : row.Table.Columns.Contains("Heating  | Heating top VAC | Temperature  (°C)")
                                                        ? float.Parse(row["Heating  | Heating top VAC | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento superior VAC | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento superior VAC | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;
                                    vacuumLowerTempf = row.Table.Columns.Contains("Heating  | Lower heating VAC | Temperature  (°C)")
                                                    ? float.Parse(row["Heating  | Lower heating VAC | Temperature  (°C)"].ToString())
                                                     : row.Table.Columns.Contains("Heating  | Heating bottom VAC | Temperature  (°C)")
                                                        ? float.Parse(row["Heating  | Heating bottom VAC | Temperature  (°C)"].ToString())
                                                    : row.Table.Columns.Contains("Calentamiento  | Calentamiento inferior VAC | Temperatura  (°C)")
                                                        ? float.Parse(row["Calentamiento  | Calentamiento inferior VAC | Temperatura  (°C)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;
                                }
                                var ppmoxigenf = row.Table.Columns.Contains("N2 / O2  | O2  | Reflow  (ppm)")
                                                    ? float.Parse(row["N2 / O2  | O2  | Reflow  (ppm)"].ToString())
                                                    : row.Table.Columns.Contains("N2 / O2  | O2  | Reflow  (ppm)")
                                                    ? float.Parse(row["N2 / O2  | O2  | Reflow  (ppm)"].ToString())
                                                    : row.Table.Columns.Contains("N2/O2  | O2  | Reflow  (ppm)")
                                                        ? float.Parse(row["N2/O2  | O2  | Reflow  (ppm)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;
                                var nitrogenreflowf = row.Table.Columns.Contains("N2 / O2  | N2  | Reflow  (%)")
                                                    ? float.Parse(row["N2 / O2  | N2  | Reflow  (%)"].ToString())
                                                    : row.Table.Columns.Contains("N2/O2  | N2  | Reflow  (%)")
                                                        ? float.Parse(row["N2/O2  | N2  | Reflow  (%)"].ToString())
                                                        : 0.0f; // Valor por defecto si ninguno de los campos existe;


                                var centersupportdb = float.Parse(parameter["CentralSupport"].ToString());
                                var conveorspeeddb = float.Parse(parameter["ConveyorSpeed"].ToString());
                                var tempzona1db = float.Parse(parameter["TempZona1"].ToString());
                                var tempzona2db = float.Parse(parameter["TempZona2"].ToString());
                                var tempzona3db = float.Parse(parameter["TempZona3"].ToString());
                                var tempzona4db = float.Parse(parameter["TempZona4"].ToString());
                                var tempzona5db = float.Parse(parameter["TempZona5"].ToString());
                                var tempzona6db = float.Parse(parameter["TempZona6"].ToString());
                                var tempzona7db = float.Parse(parameter["TempZona7"].ToString());
                                var tempzona8db = float.Parse(parameter["TempZona8"].ToString());
                                var tempzona9db = float.Parse(parameter["TempZona9"].ToString());
                                var tempzona10db = float.Parse(parameter["TempZona10"].ToString());
                                var tempzona11db = float.Parse(parameter["TempZona11"].ToString());

                                var vacuumtargetpressuredb = float.Parse(parameter["VacuumTargetPressure"].ToString());
                                var vacuumUpperTemp = float.Parse(parameter["VacuumUpperTemperature"].ToString());
                                var vacuumLowerTemp = float.Parse(parameter["VacuumLowerTemperature"].ToString());

                                var vacuumsettimedb = float.Parse(parameter["VacuumSetTime"].ToString());
                                var controlevalvedb = float.Parse(parameter["ControlValve"].ToString());
                                var holdtimedb = float.Parse(parameter["HoldTime"].ToString());
                                var ppmoxigendb = float.Parse(parameter["PPMSOxigen"].ToString());
                                var nitrogenreflowdb = float.Parse(parameter["NitrogenReflow"].ToString());

                                var toleranceconveorspeeddb = float.Parse(parameter["ToleranceConveyorSpeed"].ToString());
                                var tolerancevacuumtargetpressuredb = float.Parse(parameter["ToleranceVacuumTargetPressure"].ToString());
                                var tolerancetempzona1db = float.Parse(parameter["ToleranceTempZona1"].ToString());
                                var tolerancetempzona2db = float.Parse(parameter["ToleranceTempZona2"].ToString());
                                var tolerancetempzona3db = float.Parse(parameter["ToleranceTempZona3"].ToString());
                                var tolerancetempzona4db = float.Parse(parameter["ToleranceTempZona4"].ToString());
                                var tolerancetempzona5db = float.Parse(parameter["ToleranceTempZona5"].ToString());
                                var tolerancetempzona6db = float.Parse(parameter["ToleranceTempZona6"].ToString());
                                var tolerancetempzona7db = float.Parse(parameter["ToleranceTempZona7"].ToString());
                                var tolerancetempzona8db = float.Parse(parameter["ToleranceTempZona8"].ToString());
                                var tolerancetempzona9db = float.Parse(parameter["ToleranceTempZona9"].ToString());
                                var tolerancetempzona10db = float.Parse(parameter["ToleranceTempZona10"].ToString());
                                var tolerancetempzona11db = float.Parse(parameter["ToleranceTempZona11"].ToString());
                                var toleranceppmoxigendb = float.Parse(parameter["TolerancePPMSOxigen"].ToString());
                                var tolerancenitrogen = float.Parse(parameter["ToleranceNitrogen"].ToString());

                                var toleranceVacuumUpperTempdb = float.Parse(parameter["ToleranceVacuumUpperTemp"].ToString());
                                var toleranceVacuumLowerTempdb = float.Parse(parameter["ToleranceVacuumLowerTemp"].ToString());
                                var toleranceVacuumUpperTempmin = 0.0;
                                var toleranceVacuumUpperTempmax = 0.0;
                                var toleranceVacuumLowerTempmin = 0.0;
                                var toleranceVacuumLowerTempmax = 0.0;
                                var toleranceVacuumTargetPressuremin = 0.0;
                                var toleranceVacuumTargetPressuremax = 0.0;
                                if (vacuumactive == "True")
                                {
                                    toleranceVacuumTargetPressuremin = vacuumtargetpressuredb - tolerancevacuumtargetpressuredb;
                                    toleranceVacuumTargetPressuremax = vacuumtargetpressuredb + tolerancevacuumtargetpressuredb;

                                    toleranceVacuumUpperTempmin = vacuumUpperTemp - toleranceVacuumUpperTempdb;
                                    toleranceVacuumUpperTempmax = vacuumUpperTemp + toleranceVacuumUpperTempdb;

                                    toleranceVacuumLowerTempmin = vacuumLowerTemp - toleranceVacuumLowerTempdb;
                                    toleranceVacuumLowerTempmax = vacuumLowerTemp + toleranceVacuumLowerTempdb;
                                }
                                var toleranceconveorspeedmin = conveorspeeddb - toleranceconveorspeeddb;
                                var toleranceconveorspeedmax = conveorspeeddb + tolerancetempzona1db;

                                var tolerancetempzona1min = tempzona1db - tolerancetempzona1db;
                                var tolerancetempzona1max = tempzona1db + tolerancetempzona1db;
                                var tolerancetempzona2min = tempzona2db - tolerancetempzona2db;
                                var tolerancetempzona2max = tempzona2db + tolerancetempzona2db;
                                var tolerancetempzona3min = tempzona3db - tolerancetempzona3db;
                                var tolerancetempzona3max = tempzona3db + tolerancetempzona3db;
                                var tolerancetempzona4min = tempzona4db - tolerancetempzona4db;
                                var tolerancetempzona4max = tempzona4db + tolerancetempzona4db;
                                var tolerancetempzona5min = tempzona5db - tolerancetempzona5db;
                                var tolerancetempzona5max = tempzona5db + tolerancetempzona5db;
                                var tolerancetempzona6min = tempzona6db - tolerancetempzona6db;
                                var tolerancetempzona6max = tempzona6db + tolerancetempzona6db;
                                var tolerancetempzona7min = tempzona7db - tolerancetempzona7db;
                                var tolerancetempzona7max = tempzona7db + tolerancetempzona7db;
                                var tolerancetempzona8min = tempzona8db - tolerancetempzona8db;
                                var tolerancetempzona8max = tempzona8db + tolerancetempzona8db;
                                var tolerancetempzona9min = tempzona9db - tolerancetempzona9db;
                                var tolerancetempzona9max = tempzona9db + tolerancetempzona9db;
                                var tolerancetempzona10min = tempzona10db - tolerancetempzona10db;
                                var tolerancetempzona10max = tempzona10db + tolerancetempzona10db;
                                var tolerancetempzona11min = tempzona11db - tolerancetempzona11db;
                                var tolerancetempzona11max = tempzona11db + tolerancetempzona11db;
                                var toleranceppmoxigenmin = ppmoxigendb - toleranceppmoxigendb;
                                var toleranceppmoxigenmax = ppmoxigendb + toleranceppmoxigendb;



                                if (lbltitle.Text != "O-ERSA1026-01")
                                {
                                    if (centersupportf != centersupportdb)
                                    {
                                        messageError += $"Serial:{barcode} Program {lbltitle.Text}: Center Support out range|\nSet Point: {centersupportdb} Current: {centersupportf}\r\n";
                                        auxError++;
                                    }
                                }
                                if (vacuumactive == "True")
                                {
                                    if (vacuumtargetpressuref < toleranceVacuumTargetPressuremin || vacuumtargetpressuref > toleranceVacuumTargetPressuremax)
                                    {
                                        messageError += $"Serial:{barcode} Program {lbltitle.Text}: Vacuum Target Pressure out range|\nSet Point:{vacuumtargetpressuredb} Current:{vacuumtargetpressuref}\r\n";
                                        auxError++;
                                    }
                                    if (controlevalvef != controlevalvedb)
                                    {
                                        messageError += $"Serial:{barcode} Program {lbltitle.Text}: Control Valve out range|\nSet Point:{controlevalvedb} Current:{controlevalvef}\r\n";
                                        auxError++;
                                    }
                                    if (holdtimef != holdtimedb)
                                    {
                                        messageError += $"Serial:{barcode} Program {lbltitle.Text}: Hold time out range|\nSet Point:{holdtimedb} Current:{holdtimef}\r\n";
                                        auxError++;
                                    }
                                }
                                if (nitrogenreflowf < tolerancenitrogen || nitrogenreflowf > nitrogenreflowdb)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Nitrogen Reflow out range|\nSet Point:{nitrogenreflowdb} Current:{nitrogenreflowf}\r\n";
                                    auxError++;
                                }

                                if (conveorspeedf < toleranceconveorspeedmin || conveorspeedf > toleranceconveorspeedmax)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Conveyor Speed out range |\nSet Point:{conveorspeeddb} Current:{conveorspeedf}\r\n";
                                    auxError++;
                                }
                                if ((tempzona1f < tolerancetempzona1min || tempzona1f > tolerancetempzona1max) && tempzona1f != 0) //se agrega que la zona1 sea diferente a 0 que lo ignore
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 1 out range |\nSet Point:{tempzona1db} Current:{tempzona1f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona2f < tolerancetempzona2min || tempzona2f > tolerancetempzona2max) && tempzona2f != 0) //se agrega que la zona1 sea diferente a 0 que lo ignore
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 2 out range |\nSet Point:{tempzona2db} Current:{tempzona2f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona3f < tolerancetempzona3min || tempzona3f > tolerancetempzona3max) && tempzona3f != 0) //se agrega que la zona1 sea diferente a 0 que lo ignore
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 3 out range |\nSet Point:{tempzona3db} Current:{tempzona3f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona4f < tolerancetempzona4min || tempzona4f > tolerancetempzona4max) && tempzona4f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 4 out range |\nSet Point:{tempzona4db} Current:{tempzona4f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona5f < tolerancetempzona5min || tempzona5f > tolerancetempzona5max) && tempzona5f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 5 out range |\nSet Point:{tempzona5db} Current:{tempzona5f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona6f < tolerancetempzona6min || tempzona6f > tolerancetempzona6max) && tempzona6f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 6 out range |\nSet Point:{tempzona6db} Current:{tempzona6f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona7f < tolerancetempzona7min || tempzona7f > tolerancetempzona7max) && tempzona7f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 7 out range |\nSet Point:{tempzona7db} Current:{tempzona7f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona8f < tolerancetempzona8min || tempzona8f > tolerancetempzona8max) && tempzona8f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 8 out range |\nSet Point:{tempzona8db} Current:{tempzona8f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona9f < tolerancetempzona9min || tempzona9f > tolerancetempzona9max) && tempzona9f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 9 out range |\nSet Point:{tempzona9db} Current:{tempzona9f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona10f < tolerancetempzona10min || tempzona10f > tolerancetempzona10max) && tempzona10f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 10 out range |\nSet Point:{tempzona10db} Current:{tempzona10f}\r\n";
                                    auxError++;
                                }
                                if ((tempzona11f < tolerancetempzona11min || tempzona11f > tolerancetempzona11max) && tempzona11f != 0)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Temperature Zona 11 out range |\nSet Point:{tempzona11db} Current:{tempzona11f}\r\n";
                                    auxError++;
                                }
                                if (ppmoxigenf < toleranceppmoxigenmin || ppmoxigenf > toleranceppmoxigenmax)
                                {
                                    messageError += $"Serial:{barcode} Program {lbltitle.Text}: Nitrogen PPM Oxigen out range |\nSet Point:{ppmoxigendb} Current:{ppmoxigenf}\r\n";
                                    auxError++;
                                }
                                if (vacuumactive == "True")
                                {
                                    if (vacuumUpperTempf < toleranceVacuumUpperTempmin || vacuumUpperTempf > toleranceVacuumUpperTempmax)
                                    {
                                        messageError += $"Serial:{barcode} Program {lbltitle.Text}: Vacuum Upper Temperature out range |\nSet Point:{vacuumUpperTemp} Current:{vacuumUpperTempf}\nToleranceUpperMin:{toleranceVacuumUpperTempmin}, ToleranceUpperMax:{toleranceVacuumUpperTempmax}\r\n";
                                        auxError++;
                                    }
                                    if (vacuumLowerTempf < toleranceVacuumLowerTempmin || vacuumLowerTempf > toleranceVacuumLowerTempmax)
                                    {

                                        messageError += $"Serial:{barcode} Program {lbltitle.Text}: Vacuum Lower Temperature out range |\nSet Point:{vacuumLowerTemp} Current:{vacuumLowerTempf}\nToleranceLowerMin:{toleranceVacuumLowerTempmin}, ToleranceLowerMax:{toleranceVacuumLowerTempmax}\r\n";
                                        auxError++;
                                    }
                                }
                            }
                            if (auxError > 0)
                            {
                                //SmartFactory
                                var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                if (dtGeneralSettings.Rows.Count > 0)
                                {
                                    if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                                    {
                                        if (equipmentId == 0)
                                        {
                                            MessageBox.Show("No se encontro Id del equipo");
                                            timer1.Stop();
                                            return;
                                        }
                                        else
                                        {
                                            InsertGenericValidation(equipmentId, "ParameterValidation", "Limits out range");
                                        }
                                    }
                                }


                                results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });
                                // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                                // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                tblResults.ScrollIntoView(lastItem);
                                ProcessEvents();
                                utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", messageError);
                                error += messageError;
                                messageError = "";

                                auxErrorFile++;
                            }
                            else
                            {
                                results.Add(new Result { SerialNumber = barcode, Status = "P", ResultMessage = "Pass", Date = DateTime.Now.ToString() });
                                // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                                // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                tblResults.ScrollIntoView(lastItem);
                                ProcessEvents();
                                utils.InsertHistoryParser(barcode, equipmentName, programName, "Pass", "Success");
                            }
                        }

                    }
                    else
                    {
                        error += $"El programa {programName} no esta cargado al EPV System, Contactar al SME del proceso.";
                        auxErrorFile++;
                        results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + messageError, Date = DateTime.Now.ToString() });
                        // Obtén la referencia al último objeto en la colección de datos del DataGrid
                        var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                        // Llama al método ScrollIntoView para que el scroll se mueva al último item
                        tblResults.ScrollIntoView(lastItem);
                        ProcessEvents();
                        utils.InsertHistoryParser(barcode, equipmentName, programName, "Fail", messageError);
                        break;
                    }
                    if (auxErrorFile > 0)
                    {
                        auxError = 0;
                        if (!IsFormErrorOpen())
                        {
                            isFormErrorOpen = true;
                            frmError = new FormError();
                            frmError.Message += error;
                            frmError.Closed += (sender, args) => isFormErrorOpen = false;
                            InsertGenericValidation(equipmentId, "ParameterValidation", error);
                            UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                            frmError.Type = txtType.Text;
                            frmError.EquipmentName = equipmentName;
                            frmError.equipmentId = equipmentId;
                            frmError.Line = cmbLine.Text;
                            frmError.Show();
                        }
                        else
                        {
                            InsertGenericValidation(equipmentId, "ParameterValidation", error);
                            UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                            // Buscar el formulario FormSecundario en las ventanas abiertas
                            FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                            if (formSecundario != null)
                            {
                                // Acceder al RichTextBox en FormSecundario y agregar el texto
                                formSecundario.txtError.AppendText("\n" + error + "\n");
                            }
                            formSecundario.Activate();
                        }
                    }
                }
                if (auxErrorFile > 0)
                {
                    if (!IsFormErrorOpen())
                    {
                        isFormErrorOpen = true;
                        frmError = new FormError();
                        frmError.Message += error;
                        frmError.Closed += (sender, args) => isFormErrorOpen = false;
                        InsertGenericValidation(equipmentId, "ParameterValidation", error);
                        UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                        frmError.Type = txtType.Text;
                        frmError.EquipmentName = equipmentName;
                        frmError.equipmentId = equipmentId;
                        frmError.Line = cmbLine.Text;
                        frmError.Show();

                    }
                    else
                    {
                        InsertGenericValidation(equipmentId, "ParameterValidation", error);
                        UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                        // Buscar el formulario FormSecundario en las ventanas abiertas
                        FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                        if (formSecundario != null)
                        {
                            // Acceder al RichTextBox en FormSecundario y agregar el texto
                            formSecundario.txtError.AppendText("\n" + error + "\n");
                        }
                        formSecundario.Activate();
                    }
                }
                controller.Play();
                tblResults.CanUserResizeColumns = true;
                tblResults.ItemsSource = results;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        public class Measures
        {
            public string name { get; set; }
            public string value { get; set; }
        }
        /// <summary>
        /// Reads parameters from the specified log file path.
        /// </summary>
        /// <param name="pathLog">The path to the log file.</param>
        private void readLogWS(string pathLog)
        {
            string barcode;
            string equipmentName = "";
            int equipmentId = 0;
            string side;
            string line;
            string programName = "";
            string messageError = "";
            try
            {
                timer1.Stop();
                ClearInfo();

                var controller = ImageBehavior.GetAnimationController(ImgGif);
                var dtParser = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                var parserId = 0;
                if (dtParser.Rows.Count > 0)
                {
                    foreach (DataRow p in dtParser.Rows)
                    {
                        parserId = int.Parse(p["Parser_ID"].ToString());
                        equipmentId = int.Parse(p["Equipment_ID"].ToString());
                    }
                }
                var files = Directory.GetFiles(pathLog);

                foreach (var file in files)
                {
                    equipmentName = lbltitle.Text;
                    line = txtLine.Text;

                    controller.Pause();
                    DirectoryInfo info = new DirectoryInfo(file);

                    string csvfilePath = info.FullName.EndsWith("\\") ? info.Parent.FullName.ToString() + info.Name + ".csv" : info.Parent.FullName.ToString() + "\\" + info.Name + ".csv";
                    string[] lines = System.IO.File.ReadAllLines(info.FullName);
                    if (info.Extension == ".txt")
                    {
                        foreach (string l in lines)
                        {
                            var parts = l.Split('\t');
                            string csvLine = string.Join(",", parts);
                            Console.WriteLine(csvLine);
                            File.AppendAllText(csvfilePath, csvLine + Environment.NewLine);
                        }
                        string nameBackupFile = DateTime.Now.ToString("yyyy-MM-ddTHH_mm_ss") + info.Name;
                        File.Copy(file, txtBackupFie.Text.EndsWith("\\") ? txtBackupFie.Text + nameBackupFile : txtBackupFie.Text + "\\" + nameBackupFile);
                        File.Delete(file);
                    }

                    DataTable table = new DataTable();

                    table = CSVToDataTable(csvfilePath);


                    File.Delete(csvfilePath);
                    string error = "";
                    int auxError = 0;
                    int auxErrorFile = 0;

                    foreach (DataRow row in table.Rows)//Log
                    {
                        var dateRow = DateTime.Parse(row[0].ToString());
                        var timeRow = DateTime.Parse(row[1].ToString());

                        auxError = 0;
                        //DataTable dtParameters = GetParametersWS(row["Machine Recipe"].ToString(), row["Machine Name"].ToString());
                        DataTable dtParameters = GetParametersWS(row["Machine Recipe"].ToString(), lbltitle.Text);
                        //   DataTable dtParameters = new DataTable();
                        programName = row["Machine Recipe"].ToString();
                        barcode = row["Barcode"].ToString();

                        if (dtParameters.Rows.Count > 0)
                        {
                            foreach (DataRow item in dtParameters.Rows)//wsb
                            {
                                var toleranceph = float.Parse(item["TolerancePorcentage"].ToString());
                                var toleranceconveyorspeed = float.Parse(item["ToleranceConveyorSpeed"].ToString());
                                var toleranceconveyorwidth = float.Parse(item["ToleranceConveyorWidth"].ToString());
                                var tolerancesoldtemp = float.Parse(item["ToleranceSoldTemp"].ToString());
                                var tolerancemainwave = float.Parse(item["ToleranceMainWave"].ToString());
                                var toleranceFluxRate = 0.0;
                                var toleranceFluxVolume = 0.0;
                                var fluxFlowRate = 0.0;
                                var fluxVolume = 0.0;
                                var fluxActive = bool.Parse(item["FluxActive"].ToString());
                                if (fluxActive)
                                {
                                    toleranceFluxRate = float.Parse(item["ToleranceFluxRate"].ToString());
                                    toleranceFluxVolume = float.Parse(item["ToleranceFluxVolume"].ToString());
                                    fluxFlowRate = float.Parse(item["FluxFlowRate"].ToString());
                                    fluxVolume = float.Parse(item["FluxVolume"].ToString());
                                }
                                var conveyorSpeed = float.Parse(item["ConveyorSpeed"].ToString());
                                var conveyorWidth = float.Parse(item["ConveyorWidth"].ToString());

                                var lowph1 = float.Parse(item["LowPH1"].ToString());
                                var lowph2 = float.Parse(item["LowPH2"].ToString());
                                var lowph3 = float.Parse(item["LowPH3"].ToString());
                                var lowph4 = float.Parse(item["LowPH4"].ToString());

                                var upph1 = float.Parse(item["UpperPH1"].ToString());
                                var upph2 = float.Parse(item["UpperPH2"].ToString());
                                var upph3 = float.Parse(item["UpperPH3"].ToString());
                                var upph4 = float.Parse(item["UpperPH4"].ToString());

                                var soldtemp = float.Parse(item["SoldTemp"].ToString());
                                var mainwave = float.Parse(item["MainWave"].ToString());

                                var lph1Active = bool.Parse(item["LPH1Active"].ToString());
                                var lph2Active = bool.Parse(item["LPH2Active"].ToString());
                                var lph3Active = bool.Parse(item["LPH3Active"].ToString());
                                var lph4Active = bool.Parse(item["LPH4Active"].ToString());
                                var uph1Active = bool.Parse(item["UPH1Active"].ToString());
                                var uph2Active = bool.Parse(item["UPH2Active"].ToString());
                                var uph3Active = bool.Parse(item["UPH3Active"].ToString());
                                var uph4Active = bool.Parse(item["UPH4Active"].ToString());
                                decimal leadClearance = decimal.Parse(item["LeadClearance"].ToString());


                                var toleranceLeadClearanceMin = leadClearance - decimal.Parse(item["ToleranceLeadClearance"].ToString());
                                var toleranceLeadClearanceMax = leadClearance + decimal.Parse(item["ToleranceLeadClearance"].ToString());

                                var toleranceconveyorspeedmin = conveyorSpeed - (conveyorSpeed * (toleranceconveyorspeed / 100));
                                var toleranceconveyorspeedmax = conveyorSpeed + (conveyorSpeed * (toleranceconveyorspeed / 100));

                                var toleranceconveyorwidthmin = conveyorWidth - toleranceconveyorwidth;
                                var toleranceconveyorwidthmax = conveyorWidth + toleranceconveyorwidth;

                                var tolerancesoldtempmin = soldtemp - (soldtemp * (tolerancesoldtemp / 100));
                                var tolerancesoldtempmax = soldtemp + (soldtemp * (tolerancesoldtemp / 100));

                                var tolerancemainwavemin = mainwave - tolerancemainwave;
                                var tolerancemainwavemax = mainwave + tolerancemainwave;

                                var toleranceFluxRatemin = fluxFlowRate - (fluxFlowRate * (toleranceFluxRate / 100));
                                var toleranceFluxRatemax = fluxFlowRate + (fluxFlowRate * (toleranceFluxRate / 100));
                                var toleranceFluxVolumemin = fluxVolume - (fluxVolume * (toleranceFluxVolume / 100));
                                var toleranceFluxVolumemax = fluxVolume + (fluxVolume * (toleranceFluxVolume / 100));

                                var tolerancelowph1min = lowph1 - (lowph1 * (toleranceph / 100));
                                var tolerancelowph1max = lowph1 + (lowph1 * (toleranceph / 100));

                                var tolerancelowph2min = lowph2 - (lowph2 * (toleranceph / 100));
                                var tolerancelowph2max = lowph2 + (lowph2 * (toleranceph / 100));

                                var tolerancelowph3min = lowph3 - (lowph3 * (toleranceph / 100));
                                var tolerancelowph3max = lowph3 + (lowph3 * (toleranceph / 100));

                                var tolerancelowph4min = lowph4 - (lowph4 * (toleranceph / 100));
                                var tolerancelowph4max = lowph4 + (lowph4 * (toleranceph / 100));

                                var toleranceupperph1min = upph1 - (upph1 * (toleranceph / 100));
                                var toleranceupperph1max = upph1 + (upph1 * (toleranceph / 100));

                                var toleranceupperph2min = upph2 - (upph2 * (toleranceph / 100));
                                var toleranceupperph2max = upph2 + (upph2 * (toleranceph / 100));

                                var toleranceupperph3min = upph3 - (upph3 * (toleranceph / 100));
                                var toleranceupperph3max = upph3 + (upph3 * (toleranceph / 100));

                                var toleranceupperph4min = upph4 - (upph4 * (toleranceph / 100));
                                var toleranceupperph4max = upph4 + (upph4 * (toleranceph / 100));

                                if ((float.Parse(row["Conveyor Speed"].ToString()) < toleranceconveyorspeedmin) || (float.Parse(row["Conveyor Speed"].ToString()) > toleranceconveyorspeedmax))
                                {
                                    messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Conveyor Speed out range\nSet Point: {conveyorSpeed} Current:{float.Parse(row["Conveyor Speed"].ToString())}\r\n";
                                    auxError++;
                                }

                                if ((float.Parse(row["Solder Temperature"].ToString()) < tolerancesoldtempmin) || (float.Parse(row["Solder Temperature"].ToString()) > tolerancesoldtempmax))
                                {
                                    messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Solder Temperature out range\nSet Point: {soldtemp} Current:{float.Parse(row["Solder Temperature"].ToString())}\r\n";
                                    auxError++;
                                }
                                if ((decimal.Parse(row["Lead Clearance"].ToString()) < toleranceLeadClearanceMin) || (decimal.Parse(row["Lead Clearance"].ToString()) > toleranceLeadClearanceMax))
                                {
                                    messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}:  Lead Clearance out range\nSet Point: {leadClearance} Current:{float.Parse(row["Lead Clearance"].ToString())}\r\n";
                                    auxError++;
                                }
                                if ((float.Parse(row["Main Wave"].ToString()) < tolerancemainwavemin) || (float.Parse(row["Main Wave"].ToString()) > tolerancemainwavemax))
                                {
                                    messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}:  Main Wave out range\nSet Point: {mainwave} Current:{float.Parse(row["Main Wave"].ToString())}\r\n";
                                    auxError++;
                                }
                                if (lph1Active)
                                {
                                    if ((float.Parse(row["Lower Preheater 1 Temperature"].ToString()) < tolerancelowph1min) || (float.Parse(row["Lower Preheater 1 Temperature"].ToString()) > tolerancelowph1max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Lower Preheater 1 out range\nSet Point: {lowph1} Current:{float.Parse(row["Lower Preheater 1 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                                if (uph1Active)
                                {
                                    if ((float.Parse(row["Upper Preheater 1 Temperature"].ToString()) < toleranceupperph1min) || (float.Parse(row["Upper Preheater 1 Temperature"].ToString()) > toleranceupperph1max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Upper Preheater 1 out range\nSet Point: {upph1} Current:{float.Parse(row["Upper Preheater 1 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                                if (lph2Active)
                                {
                                    if ((float.Parse(row["Lower Preheater 2 Temperature"].ToString())) < tolerancelowph2min || (float.Parse(row["Lower Preheater 2 Temperature"].ToString()) > tolerancelowph2max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Lower Preheater 2 out range\nSet Point: {lowph2}Current:{float.Parse(row["Lower Preheater 2 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                                if (uph2Active)
                                {
                                    if ((float.Parse(row["Upper Preheater 2 Temperature"].ToString()) < toleranceupperph2min) || (float.Parse(row["Upper Preheater 2 Temperature"].ToString()) > toleranceupperph2max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Upper Preheater 2 out range\nSet Point: {upph2} Current:{float.Parse(row["Upper Preheater 2 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                                if (lph3Active)
                                {
                                    if ((float.Parse(row["Lower Preheater 3 Temperature"].ToString()) < tolerancelowph3min) || (float.Parse(row["Lower Preheater 3 Temperature"].ToString()) > tolerancelowph3max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Lower Preheater 3 out range\nSet Point: {lowph3} Current:{float.Parse(row["Lower Preheater 3 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                                if (uph3Active)
                                {
                                    if ((float.Parse(row["Upper Preheater 3 Temperature"].ToString()) < toleranceupperph3min) || (float.Parse(row["Upper Preheater 3 Temperature"].ToString()) > toleranceupperph3max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Upper Preheater 3 out range\nSet Point: {upph3} Current:{float.Parse(row["Upper Preheater 3 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }

                                if (lph4Active)

                                {
                                    if ((float.Parse(row["Lower Preheater 4 Temperature"].ToString()) < tolerancelowph4min) || (float.Parse(row["Lower Preheater 4 Temperature"].ToString()) > tolerancelowph4max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Lower Preheater 4 out range\nSet Point: {lowph4} Current:{float.Parse(row["Lower Preheater 4 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                                if (uph4Active)
                                {
                                    if ((float.Parse(row["Upper Preheater 4 Temperature"].ToString()) < toleranceupperph4min) || (float.Parse(row["Upper Preheater 4 Temperature"].ToString()) > toleranceupperph4max))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: Upper Preheater 4 out range\nSet Point: {upph4} Current:{float.Parse(row["Upper Preheater 4 Temperature"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                                if (fluxActive)
                                {
                                    if ((float.Parse(row["External Fluxer Flow Rate"].ToString()) < toleranceFluxRatemin) || (float.Parse(row["External Fluxer Flow Rate"].ToString()) > toleranceFluxRatemax))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: External Fluxer Flow Rate\nSet Point: {fluxFlowRate} Current:{float.Parse(row["External Fluxer Flow Rate"].ToString())}\r\n";
                                        auxError++;
                                    }
                                    if ((float.Parse(row["External Fluxer Flux Volume"].ToString()) < toleranceFluxVolumemin) || (float.Parse(row["External Fluxer Flux Volume"].ToString()) > toleranceFluxVolumemax))
                                    {
                                        messageError += $"Serial:{barcode} Program {row["Machine Recipe"]}: External Fluxer Flux Volume\nSet Point: {fluxVolume} Current:{float.Parse(row["External Fluxer Flux Volume"].ToString())}\r\n";
                                        auxError++;
                                    }
                                }
                            }
                            if (auxError > 0)
                            {
                                //SmartFactory
                                var dtGeneralSettings = utils.GetGeneralSettingsByParserID(parserId);
                                if (dtGeneralSettings.Rows.Count > 0)
                                {
                                    if (dtGeneralSettings.Rows[0]["UseSmartFactory"].ToString() == "True")
                                    {
                                        if (equipmentId == 0)
                                        {
                                            MessageBox.Show("No se encontro Id del equipo");
                                            timer1.Stop();
                                            return;
                                        }
                                        else
                                        {
                                            InsertGenericValidation(equipmentId, "ParameterValidation", "Limits out range");
                                        }
                                    }
                                }
                                results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = "Fail: " + messageError, Date = dateRow.ToShortDateString() + " " + timeRow.TimeOfDay });

                                utils.InsertHistoryParser1(barcode, equipmentName, programName, "Fail", messageError, dateRow.ToShortDateString() + " " + timeRow.TimeOfDay);
                                error += messageError;
                                messageError = "";
                                auxErrorFile++;
                            }
                            else
                            {
                                //int wipId = GetWipID(barcode);
                                //string resulMES = InsertWipMes(148, wipId);
                                results.Add(new Result { SerialNumber = barcode, Status = "P", ResultMessage = "Pass", Date = dateRow.ToShortDateString() + " " + timeRow.TimeOfDay });
                                // Obtén la referencia al último objeto en la colección de datos del DataGrid
                                var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                                // Llama al método ScrollIntoView para que el scroll se mueva al último item
                                tblResults.ScrollIntoView(lastItem);
                                utils.InsertHistoryParser1(barcode, equipmentName, programName, "Pass", "Success", dateRow.ToShortDateString() + " " + timeRow.TimeOfDay);
                                ProcessEvents();
                            }
                        }
                        else
                        {
                            error += $"No se encontro el programa {programName} en el sistema, favor de contactar a Ingenieria";
                            results.Add(new Result { SerialNumber = barcode, Status = "F", ResultMessage = $"No se encontro el programa {programName} en el sistema, favor de contactar a Ingenieria", Date = dateRow.ToShortDateString() + " " + timeRow.TimeOfDay });
                            // Obtén la referencia al último objeto en la colección de datos del DataGrid
                            var lastItem = tblResults.Items[tblResults.Items.Count - 1];

                            // Llama al método ScrollIntoView para que el scroll se mueva al último item
                            tblResults.ScrollIntoView(lastItem);
                            ProcessEvents();
                            utils.InsertHistoryParser1(barcode, equipmentName, programName, "F", $"No se encontro el programa {programName} en el sistema, favor de contactar a Ingenieria", dateRow.ToShortDateString() + " " + timeRow.TimeOfDay);
                            InsertGenericValidation(equipmentId, "ParameterValidation", "Limits out range");
                            auxErrorFile++;
                        }
                    }
                    if (auxErrorFile > 0)
                    {

                        auxError = 0;
                        if (!IsFormErrorOpen())
                        {
                            isFormErrorOpen = true;
                            frmError = new FormError();
                            frmError.Message += error;
                            frmError.Closed += (sender, args) => isFormErrorOpen = false;
                            InsertGenericValidation(equipmentId, "ParameterValidation", error);
                            UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                            frmError.Type = txtType.Text;
                            frmError.EquipmentName = equipmentName;
                            frmError.equipmentId = equipmentId;
                            frmError.Line = cmbLine.Text;
                            frmError.Show();

                        }
                        else
                        {
                            InsertGenericValidation(equipmentId, "ParameterValidation", error);
                            UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                            // Buscar el formulario FormSecundario en las ventanas abiertas
                            FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                            if (formSecundario != null)
                            {
                                // Acceder al RichTextBox en FormSecundario y agregar el texto
                                formSecundario.txtError.AppendText("\n" + error + "\n");
                            }
                            formSecundario.Activate();
                        }


                    }
                }

                tblResults.CanUserResizeColumns = true;
                tblResults.ItemsSource = results;
            }
            catch (Exception e)
            {
                if (!IsFormErrorOpen())
                {
                    isFormErrorOpen = true;
                    frmError = new FormError();
                    frmError.Message += e.Message;
                    frmError.Closed += (sender, args) => isFormErrorOpen = false;
                    InsertGenericValidation(equipmentId, "ParameterValidation", e.Message);
                    UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                    frmError.Type = txtType.Text;
                    frmError.EquipmentName = lbltitle.Text;
                    frmError.equipmentId = equipmentId;
                    frmError.Line = cmbLine.Text;
                    frmError.Show();

                }
                else
                {
                    InsertGenericValidation(equipmentId, "ParameterValidation", e.Message);
                    UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                    // Buscar el formulario FormSecundario en las ventanas abiertas
                    FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                    if (formSecundario != null)
                    {
                        // Acceder al RichTextBox en FormSecundario y agregar el texto
                        formSecundario.txtError.AppendText("\n" + e.Message + "\n");
                    }
                    formSecundario.Activate();
                }
            }
            timer1.Start();
        }
        /// <summary>
        /// Converts a CSV file to a DataSet, selectively populating the "tblEquipmentInfo" DataTable.
        /// </summary>
        /// <param name="path">The path to the CSV file.</param>
        /// <returns>A DataSet containing the "tblEquipmentInfo" DataTable populated with data from the CSV file.</returns>
        private DataSet CSVToDataTableSelective(string path)
        {
            DataSet ds = new DataSet();
            DataTable dtEquipment = new DataTable("tblEquipmentInfo");

            using (StreamReader sr = new StreamReader(path))
            {
                string csvData = sr.ReadToEnd();
                string[] rows = csvData.Split('\n');

                for (int i = 0; i < rows.Length - 1; i++)
                {
                    string[] rowData = rows[i].Split(';');

                    if (i == 2)
                    {
                        rowData = "Set;Hide;Description;Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Mode;Spray amount [%];Spray time [s];Tool;Z while moving [mm];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Endposition Z [mm];Speed Z [mm/s];Wave height [%];Soldering time [s];Lower value [%];Lowering time [s];Tool;Z while moving [mm];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Endposition Z [mm];Speed Z [mm/s];Wave height [%];Soldering time [s];Lower value [%];Lowering time [s];Tool;Z while moving [mm];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Endposition Z [mm];Speed Z [mm/s];Wave height [%];Soldering time [s];Lower value [%];Lowering time [s];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Mode;Spray amount [%];Spray time [s]".Split(';');

                        for (int j = 0; j < rowData.Length - 1; j++)
                        {
                            var columnName = rowData[j].Trim();

                            switch (j)
                            {
                                case int n when (n > 2 && n <= 9):
                                    columnName = rowData[j] + "f1";
                                    break;
                                case int n when (n > 9 && n <= 20):
                                    columnName = rowData[j] + "1";
                                    break;
                                case int n when (n > 20 && n <= 31):
                                    columnName = rowData[j] + "2";
                                    break;
                                case int n when (n > 31 && n <= 41):
                                    columnName = rowData[j] + "3";
                                    break;
                            }
                            dtEquipment.Columns.Add(columnName);
                        }
                    }
                    if (i > 2)
                    {
                        DataRow dr = dtEquipment.NewRow();

                        for (int k = 0; k < rowData.Length - 1; k++)
                        {
                            if (k < dtEquipment.Columns.Count)
                            {
                                dr[k] = rowData[k]?.ToString();
                            }
                        }
                        dtEquipment.Rows.Add(dr);
                    }
                }
                ds.Tables.Add(dtEquipment);
                return ds;
            }
        }

        /// <summary>
        /// Converts a CSV file to a DataSet, selectively populating the "tblEquipmentInfo" DataTable.
        /// </summary>
        /// <param name="path">The path to the CSV file.</param>
        /// <returns>A DataSet containing the "tblEquipmentInfo" DataTable populated with data from the CSV file.</returns>
        private DataSet CSVToDataTableWS(string path)
        {
            DataSet ds = new DataSet();
            DataTable dtEquipment = new DataTable("tblEquipmentInfo");

            using (StreamReader sr = new StreamReader(path))
            {
                string csvData = sr.ReadToEnd();
                string[] rows = csvData.Split('\n');

                for (int i = 0; i < rows.Length - 1; i++)
                {
                    string[] rowData = rows[i].Split(';');

                    if (i == 2)
                    {
                        rowData = "Set;Hide;Description;Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Mode;Spray amount [%];Spray time [s];Tool;Z while moving [mm];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Endposition Z [mm];Speed Z [mm/s];Wave height [%];Soldering time [s];Lower value [%];Lowering time [s];Tool;Z while moving [mm];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Endposition Z [mm];Speed Z [mm/s];Wave height [%];Soldering time [s];Lower value [%];Lowering time [s];Tool;Z while moving [mm];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Endposition Z [mm];Speed Z [mm/s];Wave height [%];Soldering time [s];Lower value [%];Lowering time [s];Endposition X [mm];Endposition Y [mm];Speed X/Y [mm/s];Mode;Spray amount [%];Spray time [s]".Split(';');

                        for (int j = 0; j < rowData.Length - 1; j++)
                        {
                            var columnName = rowData[j].Trim();

                            switch (j)
                            {
                                case int n when (n > 2 && n <= 9):
                                    columnName = rowData[j] + "f1";
                                    break;
                                case int n when (n > 9 && n <= 20):
                                    columnName = rowData[j] + "1";
                                    break;
                                case int n when (n > 20 && n <= 31):
                                    columnName = rowData[j] + "2";
                                    break;
                                case int n when (n > 31 && n <= 41):
                                    columnName = rowData[j] + "3";
                                    break;
                            }
                            dtEquipment.Columns.Add(columnName);
                        }
                    }
                    if (i > 2)
                    {
                        DataRow dr = dtEquipment.NewRow();

                        for (int k = 0; k < rowData.Length - 1; k++)
                        {
                            if (k < dtEquipment.Columns.Count)
                            {
                                dr[k] = rowData[k]?.ToString();
                            }
                        }
                        dtEquipment.Rows.Add(dr);
                    }
                }
                ds.Tables.Add(dtEquipment);
                return ds;
            }
        }


        /// <summary>
        /// Converts a CSV file to a DataSet, selectively populating multiple DataTables such as "tblEquipmentInfo", "tblMachineInfoTime", "tblMachineInfoTime2", etc.
        /// </summary>
        /// <param name="path">The path to the CSV file.</param>
        /// <returns>A DataSet containing the populated DataTables.</returns>
        private DataSet CSVToDataTableInterlux(string path)
        {
            DataSet ds = new DataSet();
            DataTable dtEquipment = new DataTable("tblEquipmentInfo");
            DataTable dtMachinInfo = new DataTable("tblMachineInfoTime");
            DataTable dtMachinInfo2 = new DataTable("tblMachineInfoTime2");
            DataTable dtBarCode = new DataTable("tblBarcode");
            DataTable dtParameters = new DataTable("tblParameters");
            DataTable dtParametersFluxSensor = new DataTable("tblParametersFluxSensor");
            DataTable dtParametersFluxSensorError = new DataTable("tblParametersFluxSensorErrorData");
            int auxfluxsensor = 0;
            int auxfluxsensorerror = 0;
            string csvData;
            using (StreamReader sr = new StreamReader(path))
            {
                csvData = sr.ReadToEnd().ToString();
                string[] row = csvData.Split('\n');
                for (int i = 0; i < row.Count() - 1; i++)
                {
                    string[] rowData = row[i].Split(';');

                    #region dtEquipment
                    if (i == 0)
                    {
                        for (int j = 0; j < rowData.Count() - 1; j++)
                        {
                            dtEquipment.Columns.Add(rowData[j].Trim());
                        }
                    }
                    else if (i == 1)
                    {
                        DataRow dr = dtEquipment.NewRow();
                        for (int k = 0; k < rowData.Count() - 1; k++)
                        {
                            dr[k] = rowData[k].ToString();
                        }
                        dtEquipment.Rows.Add(dr);
                    }
                    #endregion
                    #region dtMachinInfo
                    if (i == 6)
                    {
                        for (int j = 0; j < rowData.Count() - 1; j++)
                        {
                            dtMachinInfo.Columns.Add(rowData[j].Trim());
                        }
                    }
                    else if (i == 7)
                    {
                        DataRow dr = dtMachinInfo.NewRow();
                        for (int k = 0; k < rowData.Count() - 1; k++)
                        {
                            dr[k] = rowData[k].ToString();
                        }
                        dtMachinInfo.Rows.Add(dr);
                    }
                    #endregion
                    #region dtMachinInfo2
                    if (i == 9)
                    {
                        for (int j = 0; j < rowData.Count() - 1; j++)
                        {
                            dtMachinInfo2.Columns.Add(rowData[j].Trim());
                        }
                    }
                    else if (i == 10)
                    {
                        DataRow dr = dtMachinInfo2.NewRow();
                        for (int k = 0; k < rowData.Count() - 1; k++)
                        {
                            dr[k] = rowData[k].ToString();
                        }
                        dtMachinInfo2.Rows.Add(dr);
                    }
                    #endregion
                    #region dtBarCode
                    if (i == 12)
                    {
                        for (int j = 0; j < rowData.Count() - 1; j++)
                        {
                            dtBarCode.Columns.Add(rowData[j].Trim());
                        }
                    }
                    else if (i == 13)
                    {
                        DataRow dr = dtBarCode.NewRow();
                        for (int k = 0; k < rowData.Count() - 1; k++)
                        {
                            dr[k] = rowData[k].ToString();
                        }
                        dtBarCode.Rows.Add(dr);
                    }
                    #endregion
                    #region dtParameters
                    if (i == 15)
                    {
                        for (int j = 0; j < rowData.Count() - 1; j++)
                        {
                            dtParameters.Columns.Add(rowData[j].Trim());
                        }
                    }
                    else if (i > 15 && row[i] != "\r" && auxfluxsensor == 0 && auxfluxsensorerror == 0)
                    {
                        DataRow dr = dtParameters.NewRow();
                        for (int k = 0; k < rowData.Count() - 1; k++)
                        {
                            dr[k] = rowData[k].ToString();
                        }
                        dtParameters.Rows.Add(dr);
                    }
                    if (i > 15 && row[i] == "Flux sensor result data:\r")
                    {
                        break;
                    }
                    #endregion                    
                }


                ds.Tables.Add(dtMachinInfo);
                ds.Tables.Add(dtMachinInfo2);
                ds.Tables.Add(dtEquipment);
                ds.Tables.Add(dtBarCode);
                ds.Tables.Add(dtParameters);
                ds.Tables.Add(dtParametersFluxSensor);
                ds.Tables.Add(dtParametersFluxSensorError);
                return ds;
            }
        }
        /// <summary>
        /// Converts a CSV file to a DataTable.
        /// </summary>
        /// <param name="path">The path to the CSV file.</param>
        /// <returns>A DataTable containing the data from the CSV file.</returns>
        private DataTable CSVToDataTable(string path)
        {
            DataTable dt = new DataTable();
            try
            {

                string csvData;
                using (StreamReader sr = new StreamReader(path))
                {
                    csvData = sr.ReadToEnd().ToString();
                    string[] row = csvData.Split('\n');
                    for (int i = 0; i < row.Count() - 1; i++)
                    {
                        string[] rowData = row[i].TrimEnd().Split(',');
                        {
                            if (i == 0)
                            {
                                for (int j = 0; j < rowData.Count(); j++)
                                {
                                    dt.Columns.Add(rowData[j].Trim());
                                }
                            }
                            else
                            {
                                DataRow dr = dt.NewRow();
                                for (int k = 0; k < rowData.Count(); k++)
                                {
                                    dr[k] = rowData[k].ToString();
                                }
                                dt.Rows.Add(dr);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            return dt;
        }

        /// <summary>
        /// Inserts a generic validation entry into the MaintenanceWS service.
        /// </summary>
        /// <param name="equipmentId">The ID of the equipment.</param>
        /// <param name="validationName">The name of the validation.</param>
        /// <param name="message">The validation message.</param>
        public void InsertGenericValidation(int equipmentId, string validationName, string message)
        {
            var service = new MaintenanceWS1.MaintenanceWSSoapClient();
            try
            {
                service.InsertGenericValidation(equipmentId, validationName, message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message.ToString());
            }

        }
        /// <summary>
        /// Retrieves lines from the equipment table and populates them into a DataTable.
        /// Also adds the lines to a ComboBox.
        /// </summary>
        /// <returns>A DataTable containing the retrieved lines.</returns>
        public DataTable GetLines()
        {
            DataTable dt = new DataTable();

            try
            {
                dt = utils.getEquipment();

                var lines = from fila in dt.AsEnumerable()
                            group fila by fila.Field<string>("Line") into grupo
                            select new
                            {
                                Valor = grupo.Key,
                                Conteo = grupo.Count()
                            };
                // Añadimos los resultados al ComboBox
                foreach (var item in lines)
                {
                    cmbLine.Items.Add(item.Valor);
                }
            }
            catch (Exception ex)
            {
                // Aquí puedes manejar la excepción según tus necesidades
                throw new Exception("La cadena de conexión está vacía o no contiene datos en todas las claves.");
            }

            return dt;
        }

        /// <summary>
        /// Retrieves equipment names from the equipment table and populates them into a DataTable.
        /// Also sets up the cmbProgramName ComboBox to display the equipment names.
        /// </summary>
        /// <returns>A DataTable containing the retrieved equipment names.</returns>
        public DataTable GetEquipmentName()
        {
            DataTable dt = new DataTable();

            try
            {
                dt = utils.getEquipment();

                cmbProgramName.ItemsSource = dt.DefaultView;

                //this column will display as text
                cmbProgramName.DisplayMemberPath = dt.Columns["EquipmentName"].ToString();

                //this column will use as back end value who can you use in selectedValue property
                cmbProgramName.SelectedValuePath = dt.Columns["Equipment_ID"].ToString();
            }
            catch (Exception ex)
            {
                // Aquí puedes manejar la excepción según tus necesidades
                Console.WriteLine("Error al obtener el nombre del equipo: " + ex.Message);
            }

            return dt;
        }


        public class Result
        {
            public string SerialNumber { get; set; }
            public string Status { get; set; }
            public string ResultMessage { get; set; }
            public string Date { get; set; }
        }
        private void TextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private async void readLogPVA(string v)
        {
            string barcode;
            string equipmentName = "";
            int equipmentId = 0;
            string side;
            string line;
            string programName = "";
            string messageError = "";
            string error = "";

            try
            {
                ClearInfo();

                var controller = ImageBehavior.GetAnimationController(ImgGif);
                string currentYearMonth = DateTime.Now.ToString("yyyy-MM");

                // Buscar el archivo con el nombre que contiene el año y mes actuales
                string txtFile = Directory.GetFiles(v, $"{currentYearMonth}.txt").FirstOrDefault();

                if (txtFile != null)
                {
                    // Abrir el archivo usando FileStream con FileShare.ReadWrite
                    using (FileStream fs = new FileStream(txtFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            // Leer todas las líneas del archivo y obtener la última
                            string lastLine = null;
                            string line1 = null;
                            while ((line1 = sr.ReadLine()) != null)
                            {
                                lastLine = line1;
                            }

                            // Aquí continúa la lógica si se encuentra una última línea
                            if (lastLine != null)
                            {
                                // Buscar la posición del ".g"
                                int indexOfG = lastLine.LastIndexOf(".g");

                                if (indexOfG != -1)
                                {
                                    // Recortar desde el inicio de la cadena hasta antes del ".g"
                                    string pathPart = lastLine.Substring(0, indexOfG);

                                    // Buscar el último "\" antes de ".g"
                                    int lastBackslashIndex = pathPart.LastIndexOf("\\");

                                    if (lastBackslashIndex != -1)
                                    {
                                        // Obtener la cadena que está después del último "\" y antes del ".g"
                                        string result = pathPart.Substring(lastBackslashIndex + 1);

                                        var response = await GetCurrentSetupByEquipmentAsync("mxchim0meapps02", lbltitle.Text);
                                        string SetupSheetPN = ValidateProgramName(response, result);

                                        var dtParser = utils.GetEquipmentByEquipmentNameByLine(lbltitle.Text, txtLine.Text);
                                        var parserId = 0;
                                        if (dtParser.Rows.Count > 0)
                                        {
                                            foreach (DataRow p in dtParser.Rows)
                                            {
                                                parserId = int.Parse(p["Parser_ID"].ToString());
                                                equipmentId = int.Parse(p["Equipment_ID"].ToString());
                                            }
                                        }

                                        if (SetupSheetPN == "Ok")
                                        {
                                            Console.WriteLine("ProgramName is valid.");
                                            results.Add(new Result { SerialNumber = "N/A", Status = "P", ResultMessage = "Pass", Date = DateTime.Now.ToString() });

                                            utils.InsertHistoryParser1("NA", equipmentName, programName, "Pass", "Success", DateTime.Now.ToString());

                                            tblResults.CanUserResizeColumns = true;
                                            tblResults.ItemsSource = results;

                                            ProcessEvents();
                                        }
                                        else
                                        {
                                            error = $"Setup Sheet is not installed for the Equipment: {lbltitle.Text} ";
                                            results.Add(new Result { SerialNumber = "N/A", Status = "F", ResultMessage = "Fail: " + error, Date = DateTime.Now.ToString() });

                                            utils.InsertHistoryParser1("NA", equipmentName, programName, "Fail", error, DateTime.Now.ToString());
                                            InsertGenericValidation(equipmentId, "ParameterValidation", error);
                                            tblResults.CanUserResizeColumns = true;
                                            tblResults.ItemsSource = results;
                                            if (!IsFormErrorOpen())
                                            {
                                                frmError = new FormError();
                                                frmError.Closed += (sender, args) => isFormErrorOpen = false;

                                                UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                                                frmError.Message += error;
                                                frmError.Type = utils.Line;
                                                frmError.EquipmentName = equipmentName;
                                                frmError.equipmentId = equipmentId;
                                                frmError.Line = cmbLine.Text;
                                                frmError.Show();
                                                isFormErrorOpen = true;
                                            }
                                            else
                                            {
                                                FormError formSecundario = Application.Current.Windows.OfType<FormError>().FirstOrDefault();

                                                if (formSecundario != null)
                                                {
                                                    formSecundario.txtError.AppendText("\n" + error);
                                                    UpdateStatusParser(equipmentName, cmbLine.Text, 0, "Warning");
                                                }
                                                formSecundario.Activate();
                                            }
                                        }
                                        Console.WriteLine("Cadena extraída: " + result);
                                    }
                                    else
                                    {
                                        Console.WriteLine("No se encontró ningún '\\' antes de '.g'");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("No se encontró '.g' en la línea.");
                                }
                                Console.WriteLine("Última línea del archivo: " + lastLine);
                            }
                            else
                            {
                                Console.WriteLine("El archivo está vacío.");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"No se encontró ningún archivo {currentYearMonth}.txt en el directorio.");
                }
            }
            catch (Exception e)
            {
                lblServer.AppendText(e.Message);
            }
        }

        private void cmbProgramName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            timer1.Stop();
            string equipmentName = cmbProgramName.SelectedValue?.ToString();
            int equipmentId = 0;
            string configFilePath = ConfigurationManager.AppSettings["ConfigFilePath"];
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFilePath);
            // Leer el contenido del archivo .ini y almacenarlo en un diccionario
            Dictionary<string, string> iniData = new Dictionary<string, string>();

            string[] lines = File.ReadAllLines(fullPath);
            foreach (string line1 in lines)
            {
                if (!string.IsNullOrWhiteSpace(line1) && !line1.StartsWith(";"))
                {
                    string[] parts = line1.Split(':');
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();
                        iniData[key] = value;
                    }
                }
            }
            // Actualizar el valor de "Line" con el nuevo valor seleccionado
            iniData["EquipmentName"] = equipmentName;

            // Guardar el contenido actualizado en el archivo .ini
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[DataBase]");
            foreach (KeyValuePair<string, string> entry in iniData)
            {
                sb.AppendLine($"{entry.Key}:{entry.Value}");
            }
            File.WriteAllText(fullPath, sb.ToString());
            // Obtener el valor seleccionado en el ComboBox
            string selectedLine = cmbLine.SelectedValue.ToString();
            string line = cmbLine.SelectedValue.ToString();
            try
            {
                DataTable equipmentData = utils.GetEquipmentByEquipmentNameByLine(equipmentName, line);

                if (equipmentData == null)
                {
                    // ShowEquipmentNotConfiguredError();
                    //return;
                }
                else
                {
                    var row = equipmentData.Rows[0];
                    txtType.Text = row["Name"].ToString();
                    lbltitle.Text = row["EquipmentName"].ToString();
                    equipmentId = int.Parse(row["Equipment_ID"].ToString());
                    txtLine.Text = row["Line"].ToString();
                    lblUsername.Text = row["Username"].ToString();
                    txtPathLogFile.Text = row["PathLogFile"].ToString();
                    txtBackupFie.Text = row["PathBackUpLogFile"].ToString();
                    txtBackupFileCoords.Text = row["PathRootCoords"].ToString();
                }
            }
            catch (Exception ex)
            {
                // Handle the exception here or let it propagate up the call stack
            }
            string runningStatus = "Running";
            UpdateStatusParser(equipmentName, line, 0, runningStatus);
            timer1.Start();
            checkStatus(equipmentName, "N/A");


        }
        /// <summary>
        /// Displays an error message indicating that the equipment is not configured.
        /// </summary>
        private void ShowEquipmentNotConfiguredError()
        {
            MessageBox.Show("El equipo no esta configurado");
            btnStart.Background = Brushes.Blue;
            btnPlay.Text = "Waiting";
            timer1.Stop();
        }

        /// <summary>
        /// Event handler for the btnStart Click event.
        /// Starts the timer, changes the background color of the btnStart button to green, and updates the btnPlay text and Status.
        /// Shows a MessageBox if no equipment is selected.
        /// </summary>
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProgramName.SelectedValue != null)
            {
                timer1.Start();
                btnStart.Background = Brushes.Green;
                btnPlay.Text = "Running";
                Status = "Running";
            }
            else
            {
                MessageBoxResult result = System.Windows.MessageBox.Show("Favor de seleccionar un equipo");
            }
        }
        /// <summary>
        /// Event handler for the LoadingRow event of the tblResults DataGrid.
        /// Sets the foreground color of each row based on the "Status" property of the corresponding Result object.
        /// </summary>
        private void tblResults_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Get the DataRow corresponding to the DataGridRow that is loading.
            if (((Result)e.Row.DataContext).Status.Trim().Equals("F"))
            {
                e.Row.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                e.Row.Foreground = new SolidColorBrush(Colors.Green);
            }
        }

        private void tblResults_AddingNewItem(object sender, AddingNewItemEventArgs e)
        {


        }
        /// <summary>
        /// Event handler for the Window Closed event.
        /// If there were multiple instances of the application, it brings the main window of the first instance to the front and closes any other "FormError" processes.
        /// </summary>
        private void Window_Closed(object sender, EventArgs e)
        {
            //Si habían mas de una instancia
            if (prev_instances)
            {
                //Obtengo el proceso principal de la primera instancia de mi app
                Process p = Process.GetProcessesByName("EPVDesktopPro").Where(it => it.Id != Process.GetCurrentProcess().Id).First();
                //Muestro la ventana
                ShowWindow(p.MainWindowHandle, 1);
                //La activo y la paso a primer plano
                SetForegroundWindow(p.MainWindowHandle);


                //Obtengo el proceso principal de la primera instancia de mi app
                foreach (var process in Process.GetProcessesByName("FormError"))
                {
                    process.Kill();
                }
                //Muestro la ventana
                ShowWindow(p.MainWindowHandle, 1);
                //La activo y la paso a primer plano
                SetForegroundWindow(p.MainWindowHandle);
            }

        }
        /// <summary>
        /// Event handler for the Window Loaded event.
        /// Checks if there are multiple instances of the "EPVDesktopPro" application and closes the current window if there are.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Si hay mas de una instancia de mi aplicación
            if (Process.GetProcessesByName("EPVDesktopPro").Length > 1)
            {
                //Asigno verdadero a la variable
                prev_instances = true;
                //Cierro el formulario
                Close();
            }
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnMinimizer_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {

        }
        /// <summary>
        /// Event handler for the selection change event of the cmbLine ComboBox.
        /// Updates the UI and reads the content of the .ini file into a dictionary.
        /// </summary>
        private void cmbLine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnStart.Background = Brushes.Blue;
            btnPlay.Text = "Waiting";
            var selectedRow = cmbLine.SelectedValue;
            string configFilePath = ConfigurationManager.AppSettings["ConfigFilePath"];
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFilePath);
            // Leer el contenido del archivo .ini y almacenarlo en un diccionario
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

            // Actualizar el valor de "Line" con el nuevo valor seleccionado
            iniData["Line"] = selectedRow.ToString();

            // Guardar el contenido actualizado en el archivo .ini
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[DataBase]");
            foreach (KeyValuePair<string, string> entry in iniData)
            {
                sb.AppendLine($"{entry.Key}:{entry.Value}");
            }
            File.WriteAllText(fullPath, sb.ToString());
            // Obtener el valor seleccionado en el ComboBox
            string selectedLine = cmbLine.SelectedValue.ToString();

            try
            {
                DataTable equipmentData = utils.GetEquipmentByLine(selectedRow.ToString());
                cmbProgramName.Items.Clear();
                foreach (DataRow row in equipmentData.Rows)
                {
                    string nuevoElemento = row["EquipmentName"].ToString();
                    if (!cmbProgramName.Items.Contains(nuevoElemento))
                    {
                        cmbProgramName.Items.Add(nuevoElemento);
                        cmbProgramName.Items.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception here or let it propagate up the call stack
                Console.WriteLine(ex.Message);
            }
            return;
        }

        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            ConfigurationForm configurationForm = new ConfigurationForm();
            configurationForm.Show();
        }

        private void btnReport_Click(object sender, RoutedEventArgs e)
        {
            webForm report = new webForm();
            report.Show();
        }
    }
}
