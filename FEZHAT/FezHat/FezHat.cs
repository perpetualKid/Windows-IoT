﻿using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.Devices.Gpio;

public class FezHat
{
    private static PCA9685 pwm;
    private static I2cDevice adc;
    private static I2cDevice si7020;
    private static I2cDevice accelerometer;
    private static GpioPin onBoardLedPin;
    private static GpioPin dcMotorStandbyPin;
    private static GpioPin[] dcMotorInput1 = new GpioPin[2];
    private static GpioPin[] dcMotorInput2 = new GpioPin[2];
    private static GpioPin button1Pin;
    private static GpioPin button2Pin;

    /// <summary>
    /// Initializes the FEZ Hat's features.
    /// </summary>
    /// <returns></returns>
    public static async Task Initialize() {
        var gpioController = GpioController.GetDefault();
        var i2cControllers = await DeviceInformation.FindAllAsync(I2cDevice.GetDeviceSelector("I2C1"));

        // PWM driver
        var settings = new I2cConnectionSettings(PCA9685.Address) { BusSpeed = I2cBusSpeed.StandardMode, SharingMode = I2cSharingMode.Shared };
        pwm = new PCA9685(await I2cDevice.FromIdAsync(i2cControllers[0].Id, settings), gpioController.OpenPin(13));

        // ADC
        settings = new I2cConnectionSettings(0x48) { BusSpeed = I2cBusSpeed.StandardMode };
        adc = await I2cDevice.FromIdAsync(i2cControllers[0].Id, settings);

        // SI7020 - Humidity & Temperature
        settings = new I2cConnectionSettings(0x40) { BusSpeed = I2cBusSpeed.StandardMode };
        si7020 = await I2cDevice.FromIdAsync(i2cControllers[0].Id, settings);

        // MMA8453Q - Accelerometer
        settings = new I2cConnectionSettings(0x1C) { BusSpeed = I2cBusSpeed.StandardMode };
        accelerometer = await I2cDevice.FromIdAsync(i2cControllers[0].Id, settings);
        accelerometer.Write(new byte[2] { 0x2A, 1 });

        // On-board RED LED
        onBoardLedPin = gpioController.OpenPin(24);
        onBoardLedPin.SetDriveMode(GpioPinDriveMode.Output);

        // DC Motor

        // Set stand-by pin high
        dcMotorStandbyPin = gpioController.OpenPin(12);
        dcMotorStandbyPin.SetDriveMode(GpioPinDriveMode.Output);
        dcMotorStandbyPin.Write(GpioPinValue.High);

        // A
        dcMotorInput1[0] = gpioController.OpenPin(27);
        dcMotorInput1[0].SetDriveMode(GpioPinDriveMode.Output);
        dcMotorInput2[0] = gpioController.OpenPin(23);
        dcMotorInput2[0].SetDriveMode(GpioPinDriveMode.Output);
        DCMotor.SetDirection(DCMotorID.A, DCMotor.Direction.Clockwise);

        // B
        dcMotorInput1[1] = gpioController.OpenPin(6);
        dcMotorInput1[1].SetDriveMode(GpioPinDriveMode.Output);
        dcMotorInput2[1] = gpioController.OpenPin(5);
        dcMotorInput2[1].SetDriveMode(GpioPinDriveMode.Output);
        DCMotor.SetDirection(DCMotorID.B, DCMotor.Direction.Clockwise);

        // Buttons
        button1Pin = gpioController.OpenPin(18);
        button2Pin = gpioController.OpenPin(22);
    }

    /// <summary>
    /// Provides different colors for the LED.
    /// </summary>
    public class Color {
        /// <summary>
        /// Palette of colors.
        /// </summary>
        public enum Palette : ushort {
            Black = 0,
            Blue = 31,
            Cyan = 2047,
            Gray = 21130,
            Green = 2016,
            Magneta = 63519,
            Orange = 64480,
            Red = 63488,
            Violet = 30751,
            White = 65535,
            Yellow = 65504,
        }

        /// <summary>
        /// Get the Red, Green and Blue values from a Palette color.
        /// </summary>
        /// <param name="color">Palette color.</param>
        /// <returns>The Red, Green and Blue values.</returns>
        public static byte[] RgbFromPalette(Color.Palette color) {
            var ushortColor = (ushort)color;
            var red = (byte)(((ushortColor & 0xF800) >> 11) * 8);
            var green = (byte)(((ushortColor & 0x7E0) >> 5) * 4);
            var blue = (byte)((ushortColor & 0x1F) * 8);
            return new byte[3] { red, green, blue };
        }

        /// <summary>
        /// Get the Windows UI color from a Palette color.
        /// </summary>
        /// <param name="color">Palette color.</param>
        /// <returns>Windows UI color.</returns>
        public static Windows.UI.Color UIColorFromPalette(Color.Palette color) {
            byte[] rgb = RgbFromPalette(color);
            return Windows.UI.Color.FromArgb(255, rgb[0], rgb[1], rgb[2]);
        }
    }

    private class RgbLed {
        public int RedPin { get; set; }
        public int GreenPin { get; set; }
        public int BluePin { get; set; }

        public RgbLed(int redPin, int greenPin, int bluePin) {
            RedPin = redPin;
            GreenPin = greenPin;
            BluePin = bluePin;
        }
    }

    /// <summary>
    /// LED identifier.
    /// </summary>
    public enum LedID {
        D2 = 0,
        D3,
        DIO24
    }

    /// <summary>
    /// Controls the LEDs.
    /// </summary>
    public static class Led
    {
        private static RgbLed[] leds = new RgbLed[2];
        private static Color.Palette color;

        private static void Init() {
            if (leds[0] == null) {
                leds[0] = new RgbLed(1, 2, 0);
                leds[1] = new RgbLed(4, 15, 3);
            }
        }

        /// <summary>
        /// Turn on an LED.
        /// </summary>
        /// <param name="id">LED identifier.</param>
        public static void TurnOn(LedID id) {
            if (id == LedID.DIO24) {
                onBoardLedPin.Write(GpioPinValue.High);
            } else {
                Init();

                RgbLed led = leds[(int)id];

                var rgb = Color.RgbFromPalette(color);
                var red = Math.Abs((rgb[0] / 255.0) - .99999);
                var green = Math.Abs((rgb[1] / 255.0) - .99999);
                var blue = Math.Abs((rgb[2] / 255.0) - .99999);

                pwm.SetDutyCycle(led.RedPin, red);
                pwm.SetDutyCycle(led.GreenPin, green);
                pwm.SetDutyCycle(led.BluePin, blue);
            }
        }

        /// <summary>
        /// Turn on an LED with a specified color.
        /// </summary>
        /// <param name="id">LED identifier.</param>
        /// <param name="color">Color to use.</param>
        public static void TurnOn(LedID id, Color.Palette color) {
            Led.color = color;
            TurnOn(id);
        }

        /// <summary>
        /// Turn off all LEDs.
        /// </summary>
        public static void TurnOffAll() {
            TurnOff(LedID.D2);
            TurnOff(LedID.D3);
            TurnOff(LedID.DIO24);
        }

        /// <summary>
        /// Turn off an LED.
        /// </summary>
        /// <param name="id">LED identifier.</param>
        public static void TurnOff(LedID id)
        {
            if (id == LedID.DIO24) {
                onBoardLedPin.Write(GpioPinValue.Low);
            } else {
                Init();

                pwm.TurnOff(leds[(int)id].RedPin);
                pwm.TurnOff(leds[(int)id].GreenPin);
                pwm.TurnOff(leds[(int)id].BluePin);
            }
        }
    }

    // Simple class to wrap the PCA9685 driver (it also hides the servo methods).
    /// <summary>
    /// Handles the PWM driver chip.
    /// </summary>
    public static class Pwm {
        /// <summary>
        /// Sets the PWM's frequency.
        /// </summary>
        public static int Frequency {
            get { return pwm.Frequency; }
            set { pwm.Frequency = value; }
        }

        /// <summary>
        /// Turns output enabled on or off.
        /// </summary>
        public static bool OutputEnabled {
            get { return pwm.OutputEnabled; }
            set { pwm.OutputEnabled = value; }
        }

        /// <summary>
        /// Set's the PWM's duty cycle.
        /// </summary>
        /// <param name="channel">Channel to set.</param>
        /// <param name="dutyCycle">Duty cycle to use.</param>
        public static void SetDutyCycle(int channel, double dutyCycle) {
            pwm.SetDutyCycle(channel, dutyCycle);
        }

        /// <summary>
        /// Turn a channel on.
        /// </summary>
        /// <param name="channel">The channel to turn on.</param>
        public static void TurnOn(int channel) {
            pwm.TurnOn(channel);
        }

        /// <summary>
        /// Turn a channel off.
        /// </summary>
        /// <param name="channel">The channel to turn off.</param>
        public static void TurnOff(int channel) {
            pwm.TurnOff(channel);
        }
    }

    /// <summary>
    /// Servo motor identifier.
    /// </summary>
    public enum ServoMotorID : int {
        // New
        //S1 = 9,
        //S2 = 10
        // Old
        S1 = 8,
        S2 = 9,
        S3 = 10
    }

    /// <summary>
    /// Controls the servo motors.
    /// </summary>
    public static class ServoMotor {
        private static int frequency;
        private static double minAngle;
        private static double maxAngle;

        /// <summary>
        /// Set the movement limits of all servo motors.
        /// </summary>
        /// <param name="frequency">Frequency (Hz)</param>
        /// <param name="minPulseWidth">Minimum pulse width.</param>
        /// <param name="maxPulseWidth">Maximum pulse width.</param>
        /// <param name="minAngle">Minimum angle.</param>
        /// <param name="maxAngle">Maximum angle.</param>
        public static void Setup(int frequency, int minPulseWidth, int maxPulseWidth, double minAngle, double maxAngle) {
            ServoMotor.frequency = frequency;
            ServoMotor.minAngle = minAngle;
            ServoMotor.maxAngle = maxAngle;

            pwm.Frequency = frequency;
            pwm.SetServoLimits(minPulseWidth, maxPulseWidth, minAngle, maxAngle);
        }

        /// <summary>
        /// Set the servo motor position.
        /// </summary>
        /// <param name="id">Servo motor identifier.</param>
        /// <param name="angle">The angle to use.</param>
        public static void SetPosition(ServoMotorID id, double angle) {
            if (angle < minAngle || angle > maxAngle) throw new Exception(nameof(angle) + " must be a value between " + minAngle + " and " + maxAngle + ".");

            pwm.Frequency = frequency;
            pwm.SetServoPosition((int)id, angle);
        }

        /// <summary>
        /// Stop the servo motor from moving.
        /// </summary>
        /// <param name="id">Servo motor identifier.</param>
        public static void Stop(ServoMotorID id) {
            pwm.TurnOff((int)id);
        }
    }

    /// <summary>
    /// DC motor identifier.
    /// </summary>
    public enum DCMotorID : int {
        A = 14,
        B = 13
    }

    /// <summary>
    /// Controls the DC motors.
    /// </summary>
    public static class DCMotor {
        private static int frequency = 60;

        /// <summary>
        /// Frequency used by the DC motor.
        /// </summary>
        public static int Frequency {
            set { frequency = pwm.Frequency = value; }
            get { return frequency; }
        };

        /// <summary>
        /// Direction the motor may rotate.
        /// </summary>
        public enum Direction {
            Clockwise,
            CounterClockwise
        }

        /// <summary>
        /// Sets the rotation direction and speed.
        /// </summary>
        /// <param name="id">DC motor identifier.</param>
        /// <param name="direction">Clockwise or Counter-Clockwise.</param>
        /// <param name="speed">How fast to rotate.</param>
        public static void SetRotation(DCMotorID id, Direction direction, double speed) {
            SetSpeed(id, speed);
            SetDirection(id, direction);
        }

        /// <summary>
        /// Sets the motor's speed.
        /// </summary>
        /// <param name="id">DC motor identifier.</param>
        /// <param name="speed">The speed to use.</param>
        public static void SetSpeed(DCMotorID id, double speed) {
            pwm.Frequency = frequency;
            pwm.SetDutyCycle((int)id, speed);
        }

        /// <summary>
        /// Set the direcation the motor rotates.
        /// </summary>
        /// <param name="id">DC motor identifier.</param>
        /// <param name="direction">The direction to use.</param>
        public static void SetDirection(DCMotorID id, Direction direction) {
            pwm.Frequency = frequency;

            var i = (id == DCMotorID.A) ? 0 : 1;

            if (direction == Direction.Clockwise) {
                dcMotorInput1[i].Write(GpioPinValue.High);
                dcMotorInput2[i].Write(GpioPinValue.Low);
            } else {
                dcMotorInput1[i].Write(GpioPinValue.Low);
                dcMotorInput2[i].Write(GpioPinValue.High);
            }
        }

        /// <summary>
        /// Stop a motor from rotating.
        /// </summary>
        /// <param name="id">DC motor identifier.</param>
        public static void Stop(DCMotorID id) {
            pwm.TurnOff((int)id);
        }

        /// <summary>
        /// Stop all motors from rotating.
        /// </summary>
        public static void StopAll() {
            Stop(DCMotorID.A);
            Stop(DCMotorID.B);
        }
    }

    /// <summary>
    /// Reads the light sensor.
    /// </summary>
    public static class LightSensor {
        /// <summary>
        /// Get the brightness level.
        /// </summary>
        /// <returns>The brightness level between 0 and 1; 0 being no light and 1 being full brightness.</returns>
        public static double GetLevel() {
            return ReadAnalogByChannel(adc, 5);
        }
    }

    /// <summary>
    /// Reads the temperature and humidity.
    /// </summary>
    public static class Weather {
        private static double temperature;
        private static double humidity;

        /// <summary>
        /// What scale to use to measure the temperature.
        /// </summary>
        public enum TemperatureScale {
            Celsius,
            Fahrenheit
        }

        /// <summary>
        /// Get the analog temperature in Celsius.
        /// </summary>
        /// <returns>Temperature in Celsius.</returns>
        public static double GetAnalogTemp() {
            return GetAnalogTemp(TemperatureScale.Celsius);
        }

        /// <summary>
        /// Get the analog temperature.
        /// </summary>
        /// <param name="scale">Celsius or Fahrenheit.</param>
        /// <returns>The temperature in the desired scale.</returns>
        public static double GetAnalogTemp(TemperatureScale scale) {
            var mv = ReadAnalogByChannel(adc, 4) * 3300;
            var celsius = (mv - 450.0) / 19.5;

            if (scale == TemperatureScale.Celsius)
                return celsius;
            else
                return (celsius * 1.8) + 32;
        }

        /// <summary>
        /// Get the temperature from the SI7020 chip in Celsius.
        /// </summary>
        /// <returns>Temperature in Celsius.</returns>
        public static double GetSI7020Temp() {
            return GetSI7020Temp(TemperatureScale.Celsius);
        }

        /// <summary>
        /// Get the temperature from the SI7020 chip.
        /// </summary>
        /// <param name="scale">Celsius or Fahrenheit.</param>
        /// <returns>The temperature in the desired scale.</returns>
        public static double GetSI7020Temp(TemperatureScale scale) {
            ReadSI7020();

            if (scale == TemperatureScale.Celsius)
                return temperature;
            else
                return (temperature * 1.8) + 32;
        }

        /// <summary>
        /// Get the humidity from the SI7020 chip.
        /// </summary>
        /// <returns>Humidity</returns>
        public static double GetHumidity() {
            ReadSI7020();
            return humidity;
        }

        private static void ReadSI7020() {
            byte[] writeBuffer1 = new byte[1] { 0xE5 };
            byte[] writeBuffer2 = new byte[1] { 0xE0 };
            byte[] readBuffer1 = new byte[2];
            byte[] readBuffer2 = new byte[2];

            si7020.WriteRead(writeBuffer1, readBuffer1);
            si7020.WriteRead(writeBuffer2, readBuffer2);

            var rawRH = readBuffer1[0] << 8 | readBuffer1[1];
            var rawTemp = readBuffer2[0] << 8 | readBuffer2[1];

            temperature = 175.72 * rawTemp / 65536.0 - 46.85;
            humidity = 125.0 * rawRH / 65536.0 - 6.0;

            if (humidity < 0.0)
                humidity = 0.0;
            if (humidity > 100.0)
                humidity = 100.0;
        }
    }

    /// <summary>
    /// Reads the accelerometer.
    /// </summary>
    public static class Accelerometer {
        private static double ReadAxis(byte register) {
            byte[] data = new byte[2];
            accelerometer.WriteRead(new byte[1] { register }, data);

            double value = data[0] << 2 | data[1] >> 6;
            if ((value > 511.0))
                value -= 1024.0;
            value /= 512.0;

            return value;
        }

        /// <summary>
        /// Reads the X axis.
        /// </summary>
        /// <returns>Value representing the X axis.</returns>
        public static double ReadX() {
            return ReadAxis(0x1);
        }

        /// <summary>
        /// Reads the Y axis.
        /// </summary>
        /// <returns>Value representing the Y axis.</returns>
        public static double ReadY() {
            return ReadAxis(0x3);
        }

        /// <summary>
        /// Reads the Z axis.
        /// </summary>
        /// <returns>Value representing the Z axis.</returns>
        public static double ReadZ() {
            return ReadAxis(0x5);
        }
    }

    /// <summary>
    /// Button identifier.
    /// </summary>
    public enum ButtonID {
        DIO18 = 0,
        DIO22
    }

    /// <summary>
    /// Handles the button interaction.
    /// </summary>
    public static class Button {
        private static bool initialized = false;
        private static bool[] pressed = new bool[] { false, false };

        private static void Initalize() {
            if (!initialized) {
                if (button1Pin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                    button1Pin.SetDriveMode(GpioPinDriveMode.InputPullUp);
                else
                    button1Pin.SetDriveMode(GpioPinDriveMode.Input);

                button1Pin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
                button1Pin.ValueChanged += button1Pin_ValueChanged;

                if (button2Pin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                    button2Pin.SetDriveMode(GpioPinDriveMode.InputPullUp);
                else
                    button2Pin.SetDriveMode(GpioPinDriveMode.Input);
                
                button2Pin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
                button2Pin.ValueChanged += button2Pin_ValueChanged;

                initialized = true;
            }
        }

        /// <summary>
        /// Determines whether a button is pressed.
        /// </summary>
        /// <param name="id">Button identifier.</param>
        /// <returns>True if the button is pressed, otherwise false.</returns>
        public static bool IsPressed(ButtonID id) {
            Initalize();
            return pressed[(int)id];
        }

        private static void button1Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args) {
            pressed[0] = (args.Edge == GpioPinEdge.FallingEdge);
        }

        private static void button2Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args) {
            pressed[1] = (args.Edge == GpioPinEdge.FallingEdge);
        }
    }

    /// <summary>
    /// Expansion header pins.
    /// </summary>
    public static class Expansion {
        public enum Pin : int {
            /// <summary>
            /// Digital Input/Output (channel 0)
            /// </summary>
            DIO0 = 0,
            /// <summary>
            /// Digital Input/Output (channel 1)
            /// </summary>
            DIO1 = 1,
            /// <summary>
            /// Analog In (channel 1)
            /// </summary>
            AIn1 = 1,
            /// <summary>
            /// Analog In (channel 2)
            /// </summary>
            AIn2 = 2,
            /// <summary>
            /// Analog In (channel 3)
            /// </summary>
            AIn3 = 3,
            /// <summary>
            /// Pluse-Width Modulation (channel 5)
            /// </summary>
            PWM5 = 5,
            /// <summary>
            /// Pluse-Width Modulation (channel 6)
            /// </summary>
            PWM6 = 6,
            /// <summary>
            /// Pluse-Width Modulation (channel 7)
            /// </summary>
            PWM7 = 7,
            /// <summary>
            /// Master Output, Slave Input (output from master).
            /// </summary>
            MOSI = 19,
            /// <summary>
            /// Master Input, Slave Output (output from slave).
            /// </summary>
            MISO = 21,
            /// <summary>
            /// Serial Clock (output from master).
            /// </summary>
            SCLK = 23,
            /// <summary>
            /// Chip Select (CS)
            /// </summary>
            CS = 25,
            /// <summary>
            /// Serial Data Signal
            /// </summary>
            SDA = 3,
            /// <summary>
            /// Serial Clock
            /// </summary>
            SCL = 5
        }
    }

    /// <summary>
    /// Terminal block header pins.
    /// </summary>
    public static class TerminalBlock {
        public enum Pin : int {
            /// <summary>
            /// Analog In (channel 6)
            /// </summary>
            AIn6 = 6,
            /// <summary>
            /// Analog In (channel 7)
            /// </summary>
            AIn7 = 7,
            /// <summary>
            /// Digital Input/Output (channel 16)
            /// </summary>
            DIO16 = 16,
            /// <summary>
            /// Digital Input/Output (channel 26)
            /// </summary>
            DIO26 = 26,
            /// <summary>
            /// Pluse-Width Modulation (channel 11)
            /// </summary>
            PWM11 = 11,
            /// <summary>
            /// Pluse-Width Modulation (channel 22)
            /// </summary>
            PWM12 = 22
        }
    }

    private static double ReadAnalogByChannel(I2cDevice device, int channel) {
        if (channel < 0 || channel > 7) throw new ArgumentOutOfRangeException(nameof(channel));

        byte[] write = new byte[1] { (byte)(0x80 | 0x0C) };
        byte[] read = new byte[1];

        write[0] |= (byte)((channel % 2 == 0 ? channel / 2 : (channel - 1) / 2 + 4) << 4);

        device.WriteRead(write, read);
        return (double)read[0] / 255;
    }
}