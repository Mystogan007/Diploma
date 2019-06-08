using System;
using System.Collections.Generic;
using System.Text;

namespace HttpServer.MyServer.Basic
{
   static public class OperationID

    {
      private  static Dictionary<string, string> _operations =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        
        static  public void WriteOperationID (string operationID)
        {
            _operations[operationID] = "In process";
        }

      static  public string GetStatusOperation(string operationID)
        {
            
            string value;

            if (!_operations.TryGetValue(operationID, out value))
            {
                value = string.Empty;
            }

            return value;
        }



        static public  string GetNewOperationID(int size)
        {          
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char[] letters = "0123456789".ToCharArray();
            char ch;
            for (int i = 0; i < size; i++)
            {
                
                ch = letters[random.Next(0, letters.Length - 1)];
                //Конструируем строку со случайным символом
                builder.Append(ch);
            }
            return builder.ToString();
        }
    }
}
