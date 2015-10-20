/**
  * This library implements basic liquid crystal library
  * that communicates with the Raspberry Pi 2 or Minnowboard Max
  * through I2C
  *
  * The functionality provided by this class is similar to 
  * Arduino LiquidCrystal library.
  * 
  * This software is furnished "as is", without technical support,
  * and with no warranty, express or implied, as to its usefulness 
  * for any purpose.
  *
  * Author: Daniel Vong Wei Liang (dvwl@hotmail.com)
  * Last modified: 8 September 2015
  *
  * Credits: WIndows IoT for their I2C I/O Port Expander example 
  * (https://www.hackster.io/4803/i2c-port-expander-sample)
  */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace LCD_I2C
{
    public class I2C_LCD
    {
        private String cleanline = "";
        // commands
        private const int LCD_CLEARDISPLAY = 0x01;
        private const int LCD_RETURNHOME = 0x02;
        private const int LCD_ENTRYMODESET = 0x04;
        private const int LCDDisplayControl = 0x08;
        private const int LCD_CURSORSHIFT = 0x10;
        private const int LCD_FUNCTIONSET = 0x20;
        private const int LCD_SETCGRAMADDR = 0x40;
        private const int LCD_SETDDRAMADDR = 0x80;

        // flags for display entry mode
        private const int LCD_ENTRYRIGHT = 0x00;
        private const int LCD_ENTRYLEFT = 0x02;
        private const int LCD_ENTRYSHIFTINCREMENT = 0x01;
        private const int LCD_ENTRYSHIFTDECREMENT = 0x00;

        // flags for display on/off control
        private const int LCD_DISPLAYON = 0x04;
        private const int LCD_DISPLAYOFF = 0x00;
        private const int LCD_CURSORON = 0x02;
        private const int LCD_CURSOROFF = 0x00;
        private const int LCD_BLINKON = 0x01;
        private const int LCD_BLINKOFF = 0x00;

        // flags for display/cursor shift
        private const int LCD_DISPLAYMOVE = 0x08;
        private const int LCD_CURSORMOVE = 0x00;
        private const int LCD_MOVERIGHT = 0x04;
        private const int LCD_MOVELEFT = 0x00;

        // flags for function set
        private const int LCD_8BITMODE = 0x10;
        private const int LCD_4BITMODE = 0x00;
        private const int LCD_2LINE = 0x08;
        private const int LCD_1LINE = 0x00;
        public const int LCD_5x10DOTS = 0x04;
        public const int LCD_5x8DOTS = 0x00;

        // backlight
        private int LCD_BACKLIGHT = 0;
        private int LCD_BACKLIGHT_ON = 0x08;

        //private const string I2C_Controller_Name = "I2C5"; // For Minnowboard Max
        private const string I2C_Controller_Name = "I2C1";  // For Raspberry Pi 2

        // I2C Device declaration
        private I2cDevice I2C;

        private int DisplayFunction = 0;
        private int DisplayControl = 0;
        private int DisplayMode = 0;

        private int _addr = 0;
        private int _cols = 0;
        private int _rows = 0;
        private int _currentrow = 0;

        private String[] buffer = null;

        public bool AutoScroll = false;

        private bool isCommand = false;
        private bool isData = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void delayMicroseconds(int uS)
        {
            if (uS > 2000)
                throw new Exception("Invalid param, use Task.Delay for 2ms and more");

            if (uS < 100) //call takes more time than 100uS 
                return;

            var tick_to_reach = System.DateTime.UtcNow.Ticks + uS * 1000; //1GHz Raspi2 Clock
            while (System.DateTime.UtcNow.Ticks < tick_to_reach)
            {
            }
        }

        public I2C_LCD(int addr, int cols, int rows)
        {
            _addr = addr;
            _cols = cols;
            _rows = rows;

            buffer = new String[rows];

            for (int i = 0; i < cols; i++)
            {
                cleanline = cleanline + " ";
            }

            if (_rows > 1)
                DisplayFunction = DisplayFunction | LCD_2LINE;
            else
                DisplayFunction = DisplayFunction | LCD_1LINE;
        }

        private async Task LCD_Setup()
        {
            try
            {
                string aqs = I2cDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                if (dis.Count == 0)
                {
                    return;
                }

                var settings = new I2cConnectionSettings(_addr);
                settings.BusSpeed = I2cBusSpeed.StandardMode;
                I2C = await I2cDevice.FromIdAsync(dis[0].Id, settings);

                if (I2C == null)
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        public async Task begin()
        {
            await LCD_Setup();

            // Pull both RS and R/W low to begin commands
            I2C.Write(new byte[] { (byte)_addr, 0x00 });

            // start in 8bit mode, try to set 4 bit mode, Figure 24
            // https://www.sparkfun.com/datasheets/LCD/HD44780.pdf
            initial4bits(0x03);
            await Task.Delay(5); // wait min 4.1ms

            // second try
            initial4bits(0x03);
            await Task.Delay(5); // wait min 4.1ms

            // third go!
            initial4bits(0x03);
            delayMicroseconds(150);

            // function set (set to be 4bits long)
            initial4bits(0x02);
            delayMicroseconds(150);

            // Display off
            DisplayOff();

            // Display clear
            await clearAsync();
            delayMicroseconds(150);

            // Entry mode set
            command(LCD_ENTRYMODESET | LCD_ENTRYLEFT);
            delayMicroseconds(150);

            // Display On - Ready to go!
            DisplayOn();

            // 4-bit mode, 2 lines
            command(0x28);
            delayMicroseconds(150);

            // Turn backlight ON
            BacklightON();
        }

        // Facilitate the 4 bit nibble of Figure 24 of https://www.sparkfun.com/datasheets/LCD/HD44780.pdf
        private void initial4bits(byte message)
        {
            try
            {
                I2C.Write(new byte[] { (byte)(message << 4 | LCD_BACKLIGHT) });               // valid data comes first
                delayMicroseconds(300);
                I2C.Write(new byte[] { (byte)((message << 4 | LCD_BACKLIGHT) | 0x04) });      // En Pin HIGH 
                delayMicroseconds(300);
                I2C.Write(new byte[] { (byte)((message << 4 & ~(0x04) | LCD_BACKLIGHT)) });   // En Pin LOW
            }
            catch(Exception e)
            {
                e.ToString();
            }
        }
        // Writing to the LCD 4 bits at a time (4 bit mode)
        private void write4bits(byte message, bool registerSelect)
        {
            // RS = TRUE write to LCD (display text on LCD) => isData
            // RS = FALSE command to LCD (instruction to LCD) => isCommand
            // P0 of the I2C expander (PCF8574T) is connected to RS
            // Datasheet: http://www.nxp.com/documents/data_sheet/PCF8574.pdf

            // Send to I2C while toggling the En Pin of the LCD or P2 of the I2C expander 
            if (registerSelect)
            {
                // Data on LCD
                try
                {
                    //need to get P0 of I2C Device high
                    I2C.Write(new byte[] { (byte)(message | 0x01 | LCD_BACKLIGHT) });      // valid data comes first, RS is high
                    //delayMicroseconds(300);
                    I2C.Write(new byte[] { (byte)(message | 0x05 | LCD_BACKLIGHT) });      // En Pin HIGH
                    //delayMicroseconds(300);
                    I2C.Write(new byte[] { (byte)(message & ~(0x04) | LCD_BACKLIGHT) });   // En Pin LOW
                }
                catch (Exception e)
                {
                    e.ToString();
                }
            }
            else
            {
                // Command to LCD
                try
                {
                    //need to get P0 of I2C Device low 
                    I2C.Write(new byte[] { (byte)(message | LCD_BACKLIGHT) });             // valid data comes first
                    //delayMicroseconds(300);
                    I2C.Write(new byte[] { (byte)(message | 0x04 | LCD_BACKLIGHT) });      // En Pin HIGH 
                    //delayMicroseconds(300);
                    I2C.Write(new byte[] { (byte)(message & ~(0x04) | LCD_BACKLIGHT) });   // En Pin LOW
                }
                catch(Exception e)
                {
                    e.ToString();
                }
            }
        }

        // send data or command to LCD
        private void send(byte message, bool registerSelect)
        {
            // higher nibble
            byte temp = (byte)((message >> 4) & 0x0F);
            write4bits((byte)(temp << 4), registerSelect);
            // lower nibble
            temp = (byte)(message << 4);
            write4bits(temp, registerSelect);
        }

        // send data to LCD
        private void write(byte message)
        {
            send(message, isData);
        }

        // send command to LCD
        private void command(byte message)
        {
            send(message, isCommand);
        }

        // clear LCD
        public async Task clearAsync()
        {
            command(LCD_CLEARDISPLAY);
            await Task.Delay(2);

            for (int i = 0; i < _rows; i++)
            {
                buffer[i] = "";
            }

            _currentrow = 0;

            await homeAsync();
        }

        // home LCD
        public async Task homeAsync()
        {
            command(LCD_RETURNHOME);
            await Task.Delay(2);
        }

        // writing strings to LCD
        public void write(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);

            foreach(byte ch in data)
            {
                write(ch);
            }
        }

        // set cursor position
        public void setCursor(byte col, byte row)
        {
            var row_offsets = new int[] { 0x00, 0x40, 0x14, 0x54 };

            /*if (row >= _numlines)
            {
                row = _numlines - 1;    // we count rows starting w/0
            }
            */

            command((byte)(LCD_SETDDRAMADDR | (col + row_offsets[row])));
        }

        // Turn the backlight on/off
        public void BacklightON()
        {
            LCD_BACKLIGHT |= LCD_BACKLIGHT_ON;
        }
        public void BacklightOFF()
        {
            LCD_BACKLIGHT &= ~(LCD_BACKLIGHT_ON);
        }

        // Turn the display on/off (quickly)
        public void DisplayOn()
        {
            DisplayControl |= LCD_DISPLAYON;
            command((byte)(LCDDisplayControl | DisplayControl));
        }
        public void DisplayOff()
        {
            DisplayControl &= ~LCD_DISPLAYON;
            command((byte)(LCDDisplayControl | DisplayControl));
        }

        // Turns the underline cursor on/off
        public void noCursor()
        {
            DisplayControl &= ~LCD_CURSORON;
            command((byte)(LCDDisplayControl | DisplayControl));
        }
        public void cursor()
        {
            DisplayControl |= LCD_CURSORON;
            command((byte)(LCDDisplayControl | DisplayControl));
        }

        // Turn on and off the blinking cursor
        public void noBlink()
        {
            DisplayControl &= ~LCD_BLINKON;
            command((byte)(LCDDisplayControl | DisplayControl));
        }
        public void blink()
        {
            DisplayControl |= LCD_BLINKON;
            command((byte)(LCDDisplayControl | DisplayControl));
        }

        // These commands scroll the display without changing the RAM
        public void scrollDisplayLeft()
        {
            command(LCD_CURSORSHIFT | LCD_DISPLAYMOVE | LCD_MOVELEFT);
        }
        public void scrollDisplayRight()
        {
            command(LCD_CURSORSHIFT | LCD_DISPLAYMOVE | LCD_MOVERIGHT);
        }

        // This is for text that flows Left to Right
        public void leftToRight()
        {
            DisplayMode |= LCD_ENTRYLEFT;
            command((byte)(LCD_ENTRYMODESET | DisplayMode));
        }

        // This is for text that flows Right to Left
        public void rightToLeft()
        {
            DisplayMode &= ~LCD_ENTRYLEFT;
            command((byte)(LCD_ENTRYMODESET | DisplayMode));
        }

        // This will 'right justify' text from the cursor
        public void autoscroll()
        {
            DisplayMode |= LCD_ENTRYSHIFTINCREMENT;
            command((byte)(LCD_ENTRYMODESET | DisplayMode));
        }

        // This will 'left justify' text from the cursor
        public void noAutoscroll()
        {
            DisplayMode &= ~LCD_ENTRYSHIFTINCREMENT;
            command((byte)(LCD_ENTRYMODESET | DisplayMode));
        }

        // Allows us to fill the first 8 CGRAM locations
        // with custom characters
        public void createChar(byte location, byte[] charmap)
        {
            location &= 0x7; // we only have 8 locations 0-7
            command((byte)(LCD_SETCGRAMADDR | (location << 3)));
            for (int i = 0; i < 8; i++)
            {
                write(charmap[i]);
            }
        }

        public void WriteLine(string Text)
        {
            if (_currentrow >= _rows)
            {
                //let's do shift
                for (int i = 1; i < _rows; i++)
                {
                    buffer[i - 1] = buffer[i];
                    setCursor(0, (byte)(i - 1));
                    write(buffer[i - 1].Substring(0, _cols));
                }
                _currentrow = _rows - 1;
            }
            buffer[_currentrow] = Text + cleanline;
            setCursor(0, (byte)_currentrow);
            var cuts = buffer[_currentrow].Substring(0, _cols);
            write(cuts);
            _currentrow++;
        }
    }
}
