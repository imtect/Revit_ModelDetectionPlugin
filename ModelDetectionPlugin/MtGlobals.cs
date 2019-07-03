using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;

namespace ModelDetectionPlugin {
    public class MtGlobals {
        public const string ApplicationName = "模型检测";
        public const string DiagnosticTabName = "ModelDection";
        public const string DiagnosticPanelName = "ModelDectionPanel";

        public const string ModelDetection = "模型检测";
        public const double InchToMillimeter = 304.8f;
        public const double DefaultLevelOffset = 4000f;

        public const string PipeCategory = "管道";
        public const string DustCategory = "风管";

        public static DockablePaneId m_mainDockablePaneId = new DockablePaneId(new Guid("B9427438-A048-4088-97D7-EC0EBC92107C"));

        public enum DetectionModule {
            SpuriousConnection,
            Level,
            PipeRelation,
            BasicInfo,
            Misc
        }

        public enum BasicInfoMethods {
            MarkBasicInfo,
            WriteFloorInfo,
            MarkFloorInfo,
            SetDepColor,
            MarkFloorTag,
            AddFloorTag
        }
        public enum SpuriousConnectionMethods {
            None,
            TestSpuriousConnection
        }

        public enum LevelMethods {
            None,
            MarkVerticalPipe,
            CheckLevel,
            AutoAdjustLevel,
            CheckNoStandardSystemName,
            CheckInConsistentSystemName
        }

        public enum PipeRelationMethods {
            None,
            CheckPipeRelation,
            GetPipeRelation
        }

        public enum MiscMethods {
            None,
            GetSwitchLightRelation,
        }

        public enum Tunnel {
            [StringValue("公用")]
            Shared = 0,
            [StringValue("冬季")] //一般锅炉房内的管道
            Winter = 1,
            [StringValue("夏季")] //一般空调机组的管道
            Summer = 2 ,
            [StringValue("过渡季")]
            TransitionQuarter = 3,
        }


        public enum EPSystem {
            [StringValue("空调机房")]
            CR = 1000,
            [StringValue("空调冷热水供水")]
            WaterSupply = 1001,
            [StringValue("空调冷热水回水")]
            WaterReturn = 1002,
            [StringValue("空调冷凝水")]
            Condensate = 1003,
            [StringValue("风盘")]
            Fan = 1004,
            [StringValue("洁净空调")]
            Clean = 1005,
            [StringValue("新风")]
            Fresh = 1006,
            [StringValue("空调机房-楼层")]
            LevelCR = 1007,
            [StringValue("暖通消防")]
            Fire = 1008,
            [StringValue("排油烟")]
            OilSmokeEmission = 1009,
            [StringValue("排风")]
            Exhaust = 1010,
            [StringValue("补风")]
            AirSupply = 1011,
            [StringValue("蒸汽")]
            Steam = 1012,
            [StringValue("二氧化碳")]
            CarbanDioxide = 1400,
            [StringValue("压缩空气")]
            CompressAir = 1401,
            [StringValue("氧气")]
            Oxygen = 1402,
            [StringValue("笑气")]
            NitrousOxide = 1403,
            [StringValue("负压吸引")]
            NegativePressureAttraction = 1404,
            [StringValue("废气")]
            ExhaustGas = 1405,
            [StringValue("照明插座用电")]
            Light = 1100,
            [StringValue("其他")]
            Others = 1101,
            [StringValue("给排水消防")]
            WaterSuplyAndFireCtl = 1200,
            [StringValue("排水")]
            Drainage = 1201,
            [StringValue("生活给水")]
            LivingWaterSuply = 1202,
            [StringValue("生活热水给水")]
            LivingWaterSuplyHot = 1203,
            [StringValue("生活热水回水")]
            LivingWaterBackHot = 1204,
            [StringValue("通气系统")]
            VentilatorySystem = 1205,

        }


        public enum SystemName {
            [StringValue("暖通系统")]
            AC,
            [StringValue("给排水系统")]
            WSAD,
            [StringValue("医疗气体系统")]
            MG,
            [StringValue("蒸汽系统")]
            Steam,
        }

        public enum ACSubSystem {
            [StringValue("空调冷热水供水")]
            ac_WaterSupply,
            [StringValue("空调冷冻水供水")]
            ac_WaterFreezeSupply,
            [StringValue("空调冷热水回水")]
            ac_WaterReturn,
            [StringValue("空调冷冻水回水")]
            ac_WaterFreezeBack,
            [StringValue("冷却水供水")]
            ac_WaterCoolSupply,
            [StringValue("冷却水回水")]
            ac_WaterCoolBack,
            [StringValue("空调冷凝水")]
            ac_CondensateOfAirconditioning,
            [StringValue("冷媒管")]
            ac_RefrigerantPipe,
            [StringValue("软化水")]
            ac_SoftenedWater,
            [StringValue("定压水")]
            ac_HeadWater,
            [StringValue("膨胀水")]
            ac_SwellingWater,
            [StringValue("送风兼补风")]
            ac_SupplyingWind,
            [StringValue("消防补风")]
            ac_RegularWind,
            [StringValue("厨房补风")]
            ac_KitchenAirSupplement,
            [StringValue("平时补风")]
            ac_Firefighting,
            [StringValue("地板辐射采暖")]
            ac_RadiantFloorHeating,
            [StringValue("热风幕供水")]
            ac_HotAircurtainWaterSupply,
            [StringValue("热风幕回水")]
            ac_HotAircurtainWaterBack,
            [StringValue("一次侧热水回水")]
            ac_OnesideheatingWaterBack,
            [StringValue("一次侧热水供水")]
            ac_OnesideheatingWaterSupply,
            [StringValue("二次侧供暖回水")]
            ac_TwosideheatingWaterBack,
            [StringValue("二次侧供暖供水")]
            ac_TwosideheatingWaterSupply,
            [StringValue("风盘送风")]
            ac_WindTray,
            [StringValue("风盘回风")]
            ac_WindPlateReturnAir,
            [StringValue("风盘设备")]
            ac_AirTrayEquipment,
            [StringValue("洁净送风")]
            ac_CleanAirSupply,
            [StringValue("洁净回风")]
            ac_CleanReturnAir,
            [StringValue("洁净排风")]
            ac_CleanAirExhaus,
            [StringValue("洁净设备")]
            ac_CleanEquipmen,
            [StringValue("多样化室外机")]
            ac_DiversifiedOutdoorUnit,
            [StringValue("空调机房")]
            ac_AirconditionerRoom,
            [StringValue("送风")]
            ac_AirSupply,
            [StringValue("回风")]
            ac_AirBack,
            [StringValue("加压送风")]
            ac_Pressurization,
            [StringValue("排风")]
            ac_Exhaust,
            [StringValue("事故排风")]
            ac_AccidentExhausting,
            [StringValue("排风兼排烟")]
            ac_ExhaustAndsmokeExhaust,
            [StringValue("排烟")]
            ac_DischargeSmoke,
            [StringValue("排油烟")]
            ac_OilSmokeEmission,
            [StringValue("新风")]
            ac_AirFresh,
            [StringValue("新风兼补风")]
            ac_SupplementingWind,
            [StringValue("蒸汽供水")]
            ac_SteamSupply,
            [StringValue("蒸汽回水")]
            ac_SteamBackWater,
        }

        public enum WSADSubSystem {
            [StringValue("室内消火栓")]
            FHS,
            [StringValue("自动喷淋")]
            ASS,
            [StringValue("自动水炮灭火给水")]
            AWCF,
            [StringValue("气体灭火")]
            GFES,
            [StringValue("室外消防")]
            OFFS,
            [StringValue("重力废水")]
            GWS,
            [StringValue("压力废水")]
            PSS,
            [StringValue("污水排水")]
            GSS,
            [StringValue("厨房重力废水")]
            KGWS,
            [StringValue("通气管")]
            VLS,
            [StringValue("重力雨水")]
            GSDS,
            [StringValue("虹吸雨水")]
            SRDR,
            [StringValue("生活给水")]
            TWS,
            [StringValue("生活热水给水")]
            HWSS,
            [StringValue("直饮水给水")]
            DDWS,
            [StringValue("软化水")]
            SWS,
            [StringValue("生活热水回水")]
            HWR,
        }

        public enum SteamSubSystem {
            [StringValue("蒸汽")]
            SS,
        }

        public enum MGSubSystem {
            [StringValue("二氧化碳")]
            CDS,
            [StringValue("压缩空气")]
            CAS,
            [StringValue("负压吸引")]
            VSS,
            [StringValue("氧气")]
            OBS,
            [StringValue("氮气")]
            NS,
            [StringValue("笑气")]
            NOS,
        }



        public enum Parameters {
            [StringValue("None")]
            NoParam,
            [StringValue("标高")]
            Level,
            [StringValue("参照标高")]
            ReferenceLevel,
            [StringValue("偏移量")]
            Offset,
            [StringValue("开始偏移")]
            StartOffset,
            [StringValue("端点偏移")]
            EndOffset,
            [StringValue("楼层")]
            MtLevel,
            [StringValue("院区")]
            Distribute,
            [StringValue("建筑")]
            Building,
            [StringValue("分区")]
            SubDistrict,
            [StringValue("竖管")]
            VerticalPipe,
            [StringValue("系统名称")]
            SystemName,
            [StringValue("设备编码")]
            EquipmentCode,
            [StringValue("注释")]
            Note
        }
    }

    public class MtCommon {

        /// <summary>
        /// 将信息写入txt文件中
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        public static void WriteStringIntoText(string content, string path = null) {
            if (string.IsNullOrEmpty(path))
                path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "/temp.txt";

            if (!File.Exists(path))
                File.Create(path).Close();
            File.WriteAllText(path, content);
        }

        public static string ReadStringFromText(string path = null) {
            if (string.IsNullOrEmpty(path))
                path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "/temp.txt";
            if (File.Exists(path))
                return File.ReadAllText(path);
            else {
                return string.Empty;
            }
        }


        /// <summary>
        /// 获得一个元素所有的Connector
        /// </summary>
        /// <param name="ele"></param>
        /// <returns></returns>
        public static ConnectorSet GetAllConnectors(Element ele) {
            ConnectorSet connectors = null;

            FamilyInstance familyInstance = ele as FamilyInstance;
            if (familyInstance != null) {
                MEPModel mEPModel = familyInstance.MEPModel;
                try {
                    connectors = mEPModel.ConnectorManager.Connectors;
                } catch (Exception) {
                    TaskDialog.Show("Error", ele.Id + " ：没有找到连接点");
                    connectors = null;
                }
            } else {
                MEPCurve mepCurve = ele as MEPCurve;
                if (mepCurve != null)
                    connectors = mepCurve.ConnectorManager.Connectors;
            }
            return connectors;
        }

        /// <summary>
        /// 获得与该Connector连接的Connectors
        /// </summary>
        /// <param name="connector"></param>
        /// <returns></returns>
        public static Connector GetConnectedConnector(Connector connector) {
            Connector connectedConnector = null;
            try {
                ConnectorSet allRefs = connector.AllRefs;

                if (allRefs == null || allRefs.Size == 0)
                    return null;
                //TaskDialog.Show("Error", "No AllRefs is " + connector.Owner.Id);

                foreach (Connector conn in allRefs) {
                    if (conn.ConnectorType != ConnectorType.End ||
                        conn.Owner.Id.IntegerValue.Equals(connector.Owner.Id.IntegerValue)) {
                        continue;
                    }
                    connectedConnector = conn;
                    break;
                }

            } catch (Exception) {
                TaskDialog.Show("Error", connector.Owner.Id.ToString());
                throw;
            }
            return connectedConnector;
        }


        public static Connector GetOpenConnector(Element element, Connector inputConnector, MEPSystem system) {
            Connector openConnector = null;
            ConnectorManager cm = null;

            if (element is FamilyInstance) {
                FamilyInstance fi = element as FamilyInstance;
                cm = fi.MEPModel.ConnectorManager;
            } else {
                MEPCurve mepCurve = element as MEPCurve;
                cm = mepCurve.ConnectorManager;
            }

            foreach (Connector conn in cm.Connectors) {
                if (conn.MEPSystem == null || !conn.MEPSystem.Id.IntegerValue.Equals(system.Id.IntegerValue)) {
                    continue;
                }

                ////if (conn.MEPSystem == null || !RemoveNumInString(conn.MEPSystem.Name).Equals(RemoveNumInString(m_system.Name)))
                ////    continue;


                if (inputConnector != null && conn.IsConnectedTo(inputConnector)) {
                    continue;
                }

                if (!conn.IsConnected) {
                    openConnector = conn;
                    break;
                }

                foreach (Connector refConnector in conn.AllRefs) {
                    if (refConnector.ConnectorType != ConnectorType.End ||
                        refConnector.Owner.Id.IntegerValue.Equals(conn.Owner.Id.IntegerValue)) {
                        continue;
                    }
                    if (inputConnector != null && refConnector.Owner.Id.IntegerValue.Equals(inputConnector.Owner.Id.IntegerValue)) {
                        continue;
                    }
                    openConnector = GetOpenConnector(refConnector.Owner, conn, system);
                    if (openConnector != null) {
                        return openConnector;
                    }
                }
            }
            return openConnector;
        }

        /// <summary>
        /// 获得枚举的属性值
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static List<string> GetEnumAttributeNames(Type type) {
            List<string> attributeNames = new List<string>();
            foreach (Enum item in Enum.GetValues(type)) {
                string name = GetStringValue(item);
                attributeNames.Add(name);
            }
            return attributeNames;
        }

        /// <summary>
        /// 获得元素的参数值
        /// </summary>
        /// <param name="ele"></param>
        /// <param name="paramName"></param>
        /// <returns></returns>
        public static string GetOneParameter(Element ele, string paramName) {
            string paramValue = string.Empty;
            Parameter param;
            if (ele != null) {
                param = ele.LookupParameter(paramName);
                if (param != null) {
                    paramValue = param.AsString();

                    if (string.IsNullOrEmpty(paramValue))
                        paramValue = param.AsValueString();
                } else {
                    //paramValue = MtGlobals.Parameters.NoParam.ToString();
                }
            } else {
                //Console.WriteLine("The element is null!");
            }
            return paramValue;
        }

        /// <summary>
        /// 设置元素的参数
        /// </summary>
        /// <param name="ele"></param>
        /// <param name="paramName"></param>
        /// <param name="paramValue"></param>
        /// <returns></returns>
        public static bool SetOneParameter(Element ele, string paramName, string paramValue) {
            Parameter param;
            bool succssed = false;
            if (ele != null) {
                param = ele.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly) {
                    succssed = param.Set(paramValue);
                    if (!succssed)
                        succssed = param.SetValueString(paramValue);
                } else {
                    Console.WriteLine("The element has no " + paramName + " parameter.");
                }
            } else {
                Console.WriteLine("This element is null!");
            }
            return succssed;
        }

        /// <summary>
        /// 将mediaColor转换成Revit Color
        /// </summary>
        /// <param name="colorBrush"></param>
        /// <returns></returns>
        public static Autodesk.Revit.DB.Color TransToRevitColor(System.Windows.Media.Brush colorBrush) {
            Autodesk.Revit.DB.Color revitColor;
            System.Windows.Media.Color mediaColor = (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(colorBrush.ToString());
            revitColor = new Autodesk.Revit.DB.Color(mediaColor.R, mediaColor.G, mediaColor.B);
            return revitColor;
        }

        /// <summary>
        /// 通过ID获得元素
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Element GetElementById(Document doc, string id) {
            if (doc == null || string.IsNullOrEmpty(id))
                return null;
            ElementId elementId = new ElementId(int.Parse(id));
            Element ele = doc.GetElement(elementId);
            return ele;
        }
        /// <summary>
        /// 获取枚举的属性名称
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetStringValue(System.Enum value) {
            string output = null;
            try {
                Type type = value.GetType();
                FieldInfo fi = type.GetField(value.ToString());
                StringValue[] attrs = fi.GetCustomAttributes(typeof(StringValue), false) as StringValue[];
                if (attrs.Length > 0) {
                    output = attrs[0].Value;
                }
            } catch (Exception e) {

                throw new Exception(e.Message);
            }

            return output;
        }

        /// <summary>
        /// 获得枚举的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        public static T GetEnumValueByString<T>(string str) where T : struct {
            Type type = typeof(T);
            try {
                foreach (T value in System.Enum.GetValues(type)) {
                    string s = GetStringValue(value as System.Enum);
                    if (s == str) {
                        return value;
                    }
                }
            } catch (Exception e) {

                throw new Exception(e.Message);
            }

            return default(T);
        }

        /// <summary>
        /// 获得元素族名称
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="ele"></param>
        /// <returns></returns>
        public static string GetElementFamilyName(Document doc, Element ele) {
            if (ele == null) return null;

            ElementType elementType = doc.GetElement(ele.GetTypeId()) as ElementType;
            return elementType.FamilyName;
        }
        /// <summary>
        /// 获得元素类型名称
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="ele"></param>
        /// <returns></returns>
        public static string GetElementType(Document doc, Element ele) {
            if (ele == null) return null;

            ElementType elementType = doc.GetElement(ele.GetTypeId()) as ElementType;
            return elementType.Name;
        }

        public static void HideElementTemporary(Document doc, Element ele) {
            View3D view3d = doc.ActiveView as View3D;

            if (ele != null && !ele.IsHidden(view3d)) {
                view3d.HideElementTemporary(ele.Id);
            }
        }

        /// <summary>
        /// 隐藏元素
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="ele"></param>
        public static void HideElementsTemporary(Document doc, IList<ElementId> eles) {
            View3D view3d = doc.ActiveView as View3D;

            if (eles != null && eles.Count != 0) {
                view3d.HideElementsTemporary(eles);
            }
        }

        /// <summary>
        /// 隔离元素集合
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="eles"></param>
        public static void IsolateElements(Document doc, IList<ElementId> eles) {
            View3D view3d = doc.ActiveView as View3D;
            if (eles != null && eles.Count != 0) {
                view3d.IsolateElementsTemporary(eles);
            }
        }

        /// <summary>
        /// 去除字符串中的数字
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveNumInString(string str) {
            string strResult = string.Empty;
            try {
                strResult = Regex.Replace(str, @"\d", "").Trim();
            } catch (Exception e) {
                throw new Exception(e.Message + str);
            }
            return strResult; //去除数字
        }

        /// <summary>
        /// 去除字符串中的字符
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveNonNumInString(string str) {
            return Regex.Replace(str, @"[^\d]*", "").Trim();
        }

        /// <summary>
        /// 去除字符串中复杂的数字
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static List<string> RemoveNumInComplexString(string name) {
            List<string> list = new List<string>();
            if (!string.IsNullOrEmpty(name)) {
                if (name.Contains(",")) {
                    string[] names = name.Split(',');
                    foreach (var item in names) {
                        list.Add(RemoveNumInString(item));
                    }
                } else {
                    list.Add(RemoveNumInString(name));
                }
            }
            return list;
        }


        /// <summary>
        /// Revit版本是否为英文
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static bool IsEnglishVersion(UIApplication app) {
            LanguageType languageType = app.Application.Language;
            if (LanguageType.English_USA == languageType)
                return true;
            else
                return false;
        }


        /// <summary>
        /// 居中显示模型
        /// </summary>
        /// <param name="UIDoc"></param>
        /// <param name="ele"></param>
        public static void ElementCenterDisplay(UIDocument UIDoc, Element ele) {
            if (ele == null) return;

            XYZ point = new XYZ();
            LocationPoint locationPoint = ele.Location as LocationPoint;

            if (locationPoint != null) {
                point = locationPoint.Point;
            } else {
                LocationCurve locationCurve = ele.Location as LocationCurve;
                XYZ pointStart = locationCurve.Curve.GetEndPoint(0);
                XYZ pointEnd = locationCurve.Curve.GetEndPoint(1);
                point = (pointStart + pointEnd) / 2;
            }

            XYZ viewCornor1 = new XYZ(point.X - 10, point.Y - 10, 0);
            XYZ viewCornor2 = new XYZ(point.X + 10, point.Y + 10, 0);

            Document doc = UIDoc.Document;
            IList<UIView> views = UIDoc.GetOpenUIViews();
            UIView uiView = null;

            foreach (var uiview in views) {
                if (uiview.ViewId.Equals(doc.ActiveView.Id)) {
                    uiView = uiview;
                    break;
                }
            }
            uiView.ZoomAndCenterRectangle(viewCornor1, viewCornor2);
            UIDoc.ShowElements(ele);
        }
    }
}
