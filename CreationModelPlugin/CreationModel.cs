﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            Level level1 = GetLevel(doc,"Уровень 1");
            Level level2 = GetLevel(doc,"Уровень 2");
            double width = 10000;
            double depth = 5000;
            List<XYZ> points = CreatePoints(width, depth);
            List<Wall> walls =CreateWalls(doc, points, level1, level2);
            
            return Result.Succeeded;
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
        public List<XYZ> CreatePoints (double width, double depth)
        {
            double width1= UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double depth1= UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
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
        public List<Wall> CreateWalls(Document doc, List<XYZ> points, Level level1, Level level2 )
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
                ts.Commit();
            }
            return walls;
        }

    }

}