using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EPVDesktopPro
{
    /// <summary>
    /// Interaction logic for webForm.xaml
    /// </summary>
    public partial class webForm : Window
    {
        public webForm()
        {
            ChromiumWebBrowser web;
            InitializeComponent();
            //web = new ChromiumWebBrowser();
            //web.Load("http://mxchim0webstg83/ReportLU");
            // Agregar un controlador de evento para IsBrowserInitializedChanged
            webBrowser.IsBrowserInitializedChanged += WebBrowser_IsBrowserInitializedChanged;

        }

        private void WebBrowser_IsBrowserInitializedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (webBrowser.IsBrowserInitialized)
            {
                // Ahora puedes ejecutar código en el navegador, ya que está inicializado

                // Ejemplo: Inyectar datos en la página web cuando el navegador está inicializado
                InyectarDatosEnPaginaWeb();
            }
        }

        private void InyectarDatosEnPaginaWeb()
        {
            // Verificar si el navegador está inicializado antes de intentar ejecutar código en él
            if (webBrowser.IsBrowserInitialized)
            {
                // Obtener el equipo seleccionado del ComboBox
                string equipoSeleccionado = "WS-VE-24";

                // Crear un objeto anónimo con el equipo seleccionado
                var data = new { equipo = equipoSeleccionado };

                // Convertir el objeto a una cadena JSON
                var jsonData = JsonConvert.SerializeObject(data);

                // Ejecutar JavaScript para inyectar los datos en la página web
                webBrowser.ExecuteScriptAsync($"window.myInjectedData = {jsonData};");
            }
        }
        private void webBrowser_Loaded(object sender, RoutedEventArgs e)
        {
          

        }

        private void webBrowser_Initialized(object sender, EventArgs e)
        {
         
        }
    }
}
