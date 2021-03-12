# WIN10_LCD1602_I2C
LCD1602 interfacing with Raspberry Pi 2 Model B running on Windows 10 IoT Core via I2C.

I'm using a Funduino I2C LCD with the address of 0x27 (yours may defer).



The connections goes a like this:
LCD           Raspberry Pi 2
--------      ----------
|  GND | ---- | Pin #6 |
|  VCC | ---- | Pin #4 |
|  SDA | ---- | Pin #3 |
|  SCL | ---- | Pin #5 |
-------       ----------

https://github.com/ngducnghia/WIN10_LCD1602_I2C/blob/master/images/I2CLED1602.PNG
