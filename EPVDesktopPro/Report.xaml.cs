using ParserME;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Schema;

namespace EPVDesktopPro
{
    /// <summary>
    /// Interaction logic for Report.xaml
    /// </summary>
    public partial class Report : Window
    {

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
        Utils utils;
        ObservableCollection<ResultReport> results = new ObservableCollection<ResultReport>();


        public Report()
        {
            utils = new Utils();
            InitializeComponent();
            //webBrowser.Loaded += (s, e) =>
            //{
            //    Obtén el objeto de configuración de Internet Explorer del control WebBrowser
            //   dynamic activeX = webBrowser.GetType().InvokeMember("ActiveXInstance",
            //       BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
            //       null, webBrowser, new object[] { });

            //    Establece el modo del documento a Edge(más reciente)
            //    activeX.Silent = true;
            //    activeX.Silent = false;
            //    activeX.MSHTMLDocumentCompatibleMode = true;
            //};
         //   System.Diagnostics.Process.Start("http://localhost:1778/");
            // Luego, navega a la URL
            webBrowser.Navigate(new Uri("https://www.google.com"));
        }

     
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {




            //MainWindow mainWindow = new MainWindow();
            //string equipmentName = mainWindow.lbltitle.Text;

            //var dtReport = utils.getReportUnitsxHour(equipmentName, DateTime.Parse(startDatePicker.Text), DateTime.Parse(endDatePicker.Text));
      
            //    tblReport.ItemsSource = null;
            //    tblReport.Items.Clear();
            //    results.Clear();
                
            //    foreach (DataRow dr in dtReport.Rows)
            //    {
            //        results.Add(new ResultReport
            //        {
            //            EquipmentName = dr["EquipmentName"].ToString(),
            //            ProgramName = dr["ProgramName"].ToString(),
            //            Date =DateTime.Parse(dr["Date"].ToString()).ToShortDateString(),
            //            Hour = int.Parse(dr["Hour"].ToString()),
            //            TotalRegistros = int.Parse(dr["TotalRegistros"].ToString()),
            //            UPH = int.Parse(dr["UPH"].ToString()),
            //            TiempoDeUtilizacion = string.Format("{0:F2}%", Convert.ToDecimal(dr["TiempoDeUtilizacion"]))
            //        });
            //    }
            //    tblReport.CanUserResizeColumns = true;
            //    tblReport.ItemsSource = results;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Close();
        }

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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

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
                    Width = 300;
                    Height = 500;
                    IsMaximized = false;
                }
                else
                {
                    WindowState = WindowState.Maximized;
                    IsMaximized = true;
                }
            }
        }

        public class ResultReport
        {
            public string EquipmentName { get; set; }
            public string ProgramName { get; set; }
            public string Date { get; set; }
            public int Hour { get; set; }
            public int TotalRegistros { get; set; }
            public int UPH { get; set; }
            public string TiempoDeUtilizacion { get; set; }
        }

        private void webBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {

        }
    }
}
