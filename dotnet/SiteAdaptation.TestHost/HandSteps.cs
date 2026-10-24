using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SiteAdaptation.Common;

namespace SiteAdaptation.TestHost
{
    // R245. After a read job has read its model, the rooms and the doors of it, in metres in internal coordinates, so the hand of a
    // prototype can be told from where its entrance and its rooms sit against the internal origin. Nothing is changed or saved
    internal sealed class HandSteps
    {
        private const double SquareFeetToSquareMetres = 0.09290304;

        private readonly TestHostJob job;
        private readonly string documents;

        public HandSteps(TestHostJob job, string documents)
        {
            this.job = job;
            this.documents = documents;
        }

        public string Rooms(Document document)
        {
            var file = new HandFile { Model = System.IO.Path.GetFileName(job.Model) };
            foreach (Room room in new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().OfType<Room>())
            {
                BoundingBoxXYZ box = room.get_BoundingBox(null);
                file.Rooms.Add(new HandRoom
                {
                    ElementId = room.Id.Value,
                    Name = Text(room, BuiltInParameter.ROOM_NAME),
                    Number = room.Number,
                    Level = room.Level == null ? null : room.Level.Name,
                    AreaM2 = Math.Round(room.Area * SquareFeetToSquareMetres, 3),
                    Point = room.Location is LocationPoint point ? Metres(point.Point) : null,
                    Min = box == null ? null : Metres(box.Min),
                    Max = box == null ? null : Metres(box.Max),
                });
            }
            foreach (FamilyInstance door in new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().OfType<FamilyInstance>())
            {
                file.Doors.Add(new HandDoor
                {
                    ElementId = door.Id.Value,
                    Family = door.Symbol == null ? null : door.Symbol.FamilyName,
                    Type = door.Symbol == null ? null : door.Symbol.Name,
                    Mark = Text(door, BuiltInParameter.ALL_MODEL_MARK),
                    Level = document.GetElement(door.LevelId) is Level level ? level.Name : null,
                    Point = door.Location is LocationPoint point ? Metres(point.Point) : null,
                    Facing = Plain(door.FacingOrientation),
                    Hand = Plain(door.HandOrientation),
                    Mirrored = door.Mirrored,
                    FacingFlipped = door.FacingFlipped,
                    HandFlipped = door.HandFlipped,
                    FromRoom = RoomName(door.FromRoom),
                    ToRoom = RoomName(door.ToRoom),
                    WidthM = Width(door),
                });
            }
            JsonOut.Write(TestHostGuard.OutputPath(documents, job.Code, "rooms"), file);
            return "rooms " + file.Rooms.Count + ", doors " + file.Doors.Count;
        }

        private static string Text(Element element, BuiltInParameter which)
        {
            Parameter parameter = element.get_Parameter(which);
            return parameter == null ? null : parameter.AsString();
        }

        private static string RoomName(Room room)
        {
            return room == null ? null : Text(room, BuiltInParameter.ROOM_NAME);
        }

        private static double? Width(FamilyInstance door)
        {
            foreach (Element element in new Element[] { door, door.Symbol })
            {
                if (element == null)
                {
                    continue;
                }
                foreach (BuiltInParameter which in new[] { BuiltInParameter.DOOR_WIDTH, BuiltInParameter.FAMILY_WIDTH_PARAM })
                {
                    Parameter parameter = element.get_Parameter(which);
                    if (parameter != null && parameter.StorageType == StorageType.Double)
                    {
                        return Math.Round(UnitMath.FeetToMetres(parameter.AsDouble()), 3);
                    }
                }
            }
            return null;
        }

        private static double[] Metres(XYZ point)
        {
            return new[] { Math.Round(UnitMath.FeetToMetres(point.X), 3), Math.Round(UnitMath.FeetToMetres(point.Y), 3), Math.Round(UnitMath.FeetToMetres(point.Z), 3) };
        }

        private static double[] Plain(XYZ vector)
        {
            return vector == null ? null : new[] { Math.Round(vector.X, 4), Math.Round(vector.Y, 4), Math.Round(vector.Z, 4) };
        }
    }

    internal sealed class HandFile
    {
        public string Model { get; set; }
        public List<HandRoom> Rooms { get; set; } = new List<HandRoom>();
        public List<HandDoor> Doors { get; set; } = new List<HandDoor>();
    }

    internal sealed class HandRoom
    {
        public long ElementId { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public string Level { get; set; }
        public double AreaM2 { get; set; }
        public double[] Point { get; set; }
        public double[] Min { get; set; }
        public double[] Max { get; set; }
    }

    internal sealed class HandDoor
    {
        public long ElementId { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string Mark { get; set; }
        public string Level { get; set; }
        public double[] Point { get; set; }
        public double[] Facing { get; set; }
        public double[] Hand { get; set; }
        public bool Mirrored { get; set; }
        public bool FacingFlipped { get; set; }
        public bool HandFlipped { get; set; }
        public string FromRoom { get; set; }
        public string ToRoom { get; set; }
        public double? WidthM { get; set; }
    }
}
