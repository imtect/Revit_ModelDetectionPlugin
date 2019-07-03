using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ModelDetectionPlugin {
    public class BasicInfoError {
        public string ID { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string ErrorMsg { get; set; }
    }

    public class PipeRelationError {
        public string ID { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string ErrorMsg { get; set; }
    }

    public class LevelError {
        public string ID { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string ErrorMsg { get; set; }
    }

    public class SpuriousConnectionError {
        public string ID { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string ErrorMsg { get; set; }
    }

    public enum ErrorType {
        [StringValue("模型没有参数：")]
        NoParameter,
        [StringValue("参数未设置成功：")]
        SetParamterFailed,
        [StringValue("标高错误")]
        WrongLevel,
        [StringValue("标高偏移量为负, 标高设置偏高")] //对AC横管，为负说明楼层标高设置偏高
        NegLevelOffset,
        [StringValue("标高偏移量过高，标高设置偏低")] //对AC横管，为负说明楼层标高设置偏低
        PosLevelOffset,
        [StringValue("没有连接末端")]
        NoEndPipe,
        [StringValue("参数为空")]
        ParameterIsNull,
        [StringValue("系统名称不符合标准：")]
        NotStandardSystemName,
        [StringValue("系统名称不一致")]
        InConsistentSystemName,
        [StringValue("管道连接造成回路")]
        Circuit
    }

    public class StringValue : System.Attribute {
        private string _value;

        public StringValue(string value) {
            _value = value;
        }

        public string Value {
            get {
                return _value;
            }
        }
    }
}
