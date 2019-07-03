using System;
using System.Diagnostics;
using Autodesk.Revit.UI;

namespace ModelDetectionPlugin{
    public class MtLog {


        public static string message;
       

        public static void SetMessage(string message, int level = 0) {
            Console.WriteLine(message);
            Debug.WriteLine(message);
            if (level > 0)
                TaskDialog.Show("Revit", message);
        }
    }
}
