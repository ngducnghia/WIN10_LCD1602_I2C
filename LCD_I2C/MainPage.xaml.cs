using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace LCD_I2C
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // slave address is 0x27 on a funduino I2C LCD
        private I2C_LCD LCD = new I2C_LCD(0x27, 16, 2);

        public MainPage()
        {
            this.InitializeComponent();

            InitI2CLCD();
        }

        private async void InitI2CLCD()
        {
            try
            {               
                await LCD.begin();

                await LCD.homeAsync();

                LCD.write("Hello World!");
                status_TB.Text = "Status: Hello World!";
            }
            catch (Exception e)
            {
                status_TB.Text = "Error Status: " + e.Message;
                return;
            }
        }

        private async void sendToLCD_Click(object sender, RoutedEventArgs e)
        {
            await LCD.clearAsync();

            LCD.setCursor(0, 0);
            LCD.write(input_TB.Text);

            if (input_TB.Text.Length > 16)
            {
                string cut = input_TB.Text.Substring(16);
                LCD.setCursor(0, 1);
                LCD.write(cut);
            }
        }
    }
}
