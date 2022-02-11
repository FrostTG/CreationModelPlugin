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
            CreateHouse(doc, points, level1, level2, width, depth);

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
        public List<Wall> CreateHouse(Document doc, List<XYZ> points, Level level1, Level level2, double width, double depth)
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
                AddRoof(doc, level2, walls, width, depth);
                ts.Commit();
            }
            return walls;
        }

        private void AddRoof(Document doc, Level level2, List<Wall> walls, double width, double depth)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                 .OfClass(typeof(RoofType))
                 .OfType<RoofType>()
                 .Where(x => x.Name.Equals("Типовой - 400мм"))
                 .Where(x => x.FamilyName.Equals("Базовая крыша"))
                 .FirstOrDefault();
            View view = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .OfType<View>()
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();
            #region NewExtrusionRoof рабочий варинат без привязки
            //CurveArray curveArray = new CurveArray();
            //curveArray.Append(Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 20, 20)));
            //curveArray.Append(Line.CreateBound(new XYZ(0, 20, 20), new XYZ(0, 40, 0)));
            //ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            //doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, 0, 40);
            #endregion
            #region NewExtrusionRoof ошибка при построении
            //double wallWidth = walls[0].Width;
            //double dt = wallWidth / 2;

            //double exrtusionStart = -width / 2 - dt;
            //double extrusionEnd = width / 2 + dt;

            //double curveStrart = -depth / 2 - dt;
            //double curveEnd = depth / 2 + dt;

            //CurveArray curveArray = new CurveArray();
            //curveArray.Append(Line.CreateBound(new XYZ(0, curveStrart, level2.Elevation), new XYZ(0, 0, level2.Elevation + 10)));
            //curveArray.Append(Line.CreateBound(new XYZ(0, 0, level2.Elevation + 10), new XYZ(0, curveEnd, level2.Elevation)));
            //ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            //ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, exrtusionStart, extrusionEnd);
            //extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;
            #endregion
            #region Нахождение через точки
            LocationCurve hostCurve = walls[1].Location as LocationCurve;
            XYZ point3 = hostCurve.Curve.GetEndPoint(0);
            XYZ point4 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point3 + point4) / 2;
            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;
            XYZ point1 = new XYZ(-dt, -dt, level2.Elevation);
            XYZ point2 = new XYZ(dt, dt, level2.Elevation);
            XYZ A = point3 + point1;
            XYZ B = new XYZ(point.X, point.Y, 20);
            XYZ C = point4 + point2;
            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(A, B));
            curveArray.Append(Line.CreateBound(B, C));

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, -A.X-wallWidth, A.X+wallWidth);
            extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;
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
