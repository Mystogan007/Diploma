using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HttpServer.MyServer.Support
{
    static public class Pinvoke
    {
        [DllImport(@"control.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint ControlSystemEntryPoint(
            uint ID,
            IntPtr in_params,
            uint in_byte_count,
          out IntPtr out_params,
           out uint out_byte_count);

        [DllImport(@"D:\Хлам с рабочего стола\V\HttpServer\bin\Debug\netcoreapp2.1\control.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ControlSystemGetErrorDescription(
        uint code,
        out uint pSize );
        public static string GetEntryPointToLoadModel(byte[] arrayModel)
        {
            IntPtr in_params = Marshal.AllocHGlobal(arrayModel.Length);
            Marshal.Copy(arrayModel, 0, in_params, arrayModel.Length);
            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(2, in_params, (uint)arrayModel.Length, out out_params, out out_byte_count);
            if (a == 0)
            {
                Marshal.FreeHGlobal(in_params);
                byte[] temp = new byte[out_byte_count];
                Marshal.Copy(out_params, temp, 0, (int)out_byte_count);
                ////   Marshal.FreeHGlobal(out_params);
                return Encoding.UTF8.GetString(temp);
            }
            else
            {
                uint pSize;
                IntPtr error = ControlSystemGetErrorDescription(a, out pSize);
                var result = Marshal.PtrToStringUTF8(error);
                return result;
            }
        }

        public static string GetEntryPointToStartModeling(Dictionary<string, string> options)
        {
            byte[] subjectArray = Encoding.UTF8.GetBytes(options["typeOfSubjectArea"]);
            byte[] subjectArrayLength = BitConverter.GetBytes(subjectArray.Length);
            byte[] nameOfModel = Encoding.UTF8.GetBytes(options["nameOfModelToStartSimulation"]);
            byte[] nameOfModelLength = BitConverter.GetBytes(nameOfModel.Length);
            byte[] requestLine = new byte[subjectArray.Length + subjectArrayLength.Length + nameOfModelLength.Length + nameOfModel.Length];

            int k = 0;
            foreach (byte i in subjectArrayLength)
            {
                requestLine[k] = i;
                k++;
            }
            foreach (byte i in subjectArray)
            {
                requestLine[k] = i;
                k++;
            }
            foreach (byte i in nameOfModelLength)
            {
                requestLine[k] = i;
                k++;
            }
            foreach (byte i in nameOfModel)
            {
                requestLine[k] = i;
                k++;
            }

            IntPtr in_params = Marshal.AllocHGlobal(requestLine.Length);
            Marshal.Copy(requestLine, 0, in_params, requestLine.Length);
            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(3, in_params, (uint)requestLine.Length, out out_params, out out_byte_count);

            if (a == 0)
            {
                Marshal.FreeHGlobal(in_params);
                byte[] temp = new byte[out_byte_count];
                Marshal.Copy(out_params, temp, 0, (int)out_byte_count);
                //   Marshal.FreeHGlobal(out_params);
                return Encoding.UTF8.GetString(temp);
            }
            else
            {
                uint pSize;
                IntPtr error = ControlSystemGetErrorDescription(a, out pSize);
                var result = Marshal.PtrToStringUTF8(error);
                return result;
            }

        }

        public static string GetEntryPointToCheckStatus(Dictionary<string, string> options)
        {
            byte[] nameOfModel = Encoding.UTF8.GetBytes(options["nameOfModelToStartSimulation"]);
            byte[] nameOfModelLength = BitConverter.GetBytes(nameOfModel.Length);
            byte[] requestLine = new byte[nameOfModelLength.Length + nameOfModel.Length];

            int k = 0;
            foreach (byte i in nameOfModelLength)
            {
                requestLine[k] = i;
                k++;
            }
            foreach (byte i in nameOfModel)
            {
                requestLine[k] = i;
                k++;
            }

            IntPtr in_params = Marshal.AllocHGlobal(requestLine.Length);
            Marshal.Copy(requestLine, 0, in_params, requestLine.Length);
            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(4, in_params, (uint)requestLine.Length, out out_params, out out_byte_count);

            if (a == 0)
            {
                Marshal.FreeHGlobal(in_params);
                byte[] temp = new byte[out_byte_count];
                Marshal.Copy(out_params, temp, 0, (int)out_byte_count);
                //   Marshal.FreeHGlobal(out_params);
                return Encoding.UTF8.GetString(temp);
            }
            else
            {
                uint pSize;
                IntPtr error = ControlSystemGetErrorDescription(a, out pSize);
                var result = Marshal.PtrToStringUTF8(error);
                return result;
            }

        }




    }
}
