using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            Level level1 = GetLevel(doc, "Уровень 1");
            Level level2 = GetLevel(doc, "Уровень 2");
            double width = 10000;
            double depth = 5000;
            List<XYZ> points = CreatePoints(width, depth);
            List<Wall> walls = CreateHouse(doc, points, level1, level2);

            return Result.Succeeded;
        }

        private void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("750 x 1200 мм"))
                .Where(x => x.Family.Name.Equals("M_Подъемное-двустворчатое-окно"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point2 + point1) / 2;

            if (!windowType.IsActive)
                windowType.Activate();
            FamilyInstance familyWindow = doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);
            double headHeight = familyWindow.get_Parameter(BuiltInParameter.INSTANCE_HEAD_HEIGHT_PARAM).AsDouble();
            double sillHeight = familyWindow.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).AsDouble();
            double wallHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            familyWindow.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(wallHeight / 2 - (headHeight - sillHeight) / 2);

        }

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                 .OfClass(typeof(FamilySymbol))
                 .OfCategory(BuiltInCategory.OST_Doors)
                 .OfType<FamilySymbol>()
                 .Where(x => x.Name.Equals("0915 x 2134 мм"))
                 .Where(x => x.Family.Name.Equals("M_Однопольные-Щитовые"))
                 .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();
            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
        }

        public Level GetLevel(Document doc, string levelName)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            Level level = listLevel
                .Where(x => x.Name.Equals(levelName))
                .FirstOrDefault();
            return level;
        }
        public List<XYZ> CreatePoints(double width, double depth)
        {
            double width1 = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double depth1 = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
            double dx = width1 / 2;
            double dy = depth1 / 2;
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));
            return points;
        }
        public List<Wall> CreateHouse(Document doc, List<XYZ> points, Level level1, Level level2)
        {
            List<Wall> walls = new List<Wall>();

            using (Transaction ts = new Transaction(doc, "Create wall"))
            {
                ts.Start();

                for (int i = 0; i < 4; i++)
                {
                    Line line = Line.CreateBound(points[i], points[i + 1]);
                    Wall wall = Wall.Create(doc, line, level1.Id, false);
                    walls.Add(wall);
                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
                }
                AddDoor(doc, level1, walls[0]);
                AddWindow(doc, level1, walls[1]);
                AddWindow(doc, level1, walls[2]);
                AddWindow(doc, level1, walls[3]);
                AddRoof(doc, level2, walls);
                ts.Commit();
            }
            return walls;
        }

        private void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                 .OfClass(typeof(RoofType))
                 .OfType<RoofType>()
                 .Where(x => x.Name.Equals("Типовой - 400мм"))
                 .Where(x => x.FamilyName.Equals("Базовая крыша"))
                 .FirstOrDefault();
            #region NewExtrusionRoof рабочий варинат без привязки
            //CurveArray curveArray = new CurveArray();
            //curveArray.Append(Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 20, 20)));
            //curveArray.Append(Line.CreateBound(new XYZ(0, 20, 20), new XYZ(0, 40, 0)));
            //ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            //doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, 0, 40);
            #endregion
            #region NewExtrusionRoof

            Application application1 = doc.Application;
            CurveArray curveArray = application1.Create.NewCurveArray();
            LocationCurve curve1 = walls[1].Location as LocationCurve;
            XYZ p1 = curve1.Curve.GetEndPoint(0);
            XYZ p2 = curve1.Curve.GetEndPoint(1);
            XYZ p3 = (p1 + p2) / 2;
            LocationCurve curve2 = walls[3].Location as LocationCurve;
            XYZ p4 = curve1.Curve.GetEndPoint(0);
            XYZ p5 = curve1.Curve.GetEndPoint(1);
            XYZ p6 = (p4 + p5) / 2;
            LocationCurve curve3 = walls[0].Location as LocationCurve;
            XYZ p7 = curve1.Curve.GetEndPoint(0);
            XYZ p8 = curve1.Curve.GetEndPoint(1);
            XYZ p9 = (p7 + p8) / 2;
            LocationCurve curve4 = walls[2].Location as LocationCurve;
            XYZ p10 = curve1.Curve.GetEndPoint(0);
            XYZ p11 = curve1.Curve.GetEndPoint(1);
            XYZ p12 = (p10 + p11) / 2;
            //Line line1 = Line.CreateBound(p9, p12);
            XYZ Z = (p9 + p12) / 2;
            XYZ N = new XYZ(0, 0, 20);
            XYZ T = Z + N;
            CurveArray curveMain = new CurveArray();
            curveMain.Append(Line.CreateBound(p3, T));
            curveMain.Append(Line.CreateBound(T, p6));
            ReferencePlane plane = doc.Create.NewReferencePlane(p3, T, p6, doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveMain, plane, level2, roofType, 0, 40);

            #endregion
            #region NewFootPrintRoof
            //double wallWidth = walls[0].Width;
            //double dt = wallWidth / 2;          

            //List<XYZ> points = new List<XYZ>();
            //points.Add(new XYZ(-dt, -dt, 0));
            //points.Add(new XYZ(dt, -dt, 0));
            //points.Add(new XYZ(dt, dt, 0));
            //points.Add(new XYZ(-dt, dt, 0));
            //points.Add(new XYZ(-dt, -dt, 0));

            //Application application = doc.Application;
            //CurveArray footprint = application.Create.NewCurveArray();
            //for (int i = 0; i < 4; i++)
            //{
            //    LocationCurve curve = walls[i].Location as LocationCurve;
            //    XYZ point1 = curve.Curve.GetEndPoint(0);
            //    XYZ point2 = curve.Curve.GetEndPoint(1);
            //    Line line = Line.CreateBound(point1 + points[i], point2 + points[i + 1]);
            //    footprint.Append(line);
            //}
            //ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
            //FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);

            //foreach (ModelCurve m in footPrintToModelCurveMapping)
            //{
            //    footprintRoof.set_DefinesSlope(m, true);
            //    footprintRoof.set_SlopeAngle(m, 0.5);
            //}
            #endregion
        }
    }

}
