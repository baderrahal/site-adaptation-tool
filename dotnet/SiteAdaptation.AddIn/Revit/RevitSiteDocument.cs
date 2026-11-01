using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using SiteAdaptation.Common;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.AddIn.Revit
{
    // R373, R374. The Revit side of button 1: it reads the document into a SiteState and carries out the steps SetUpRun decides, in
    // feet and radians inside, metres and degrees out. The API calls are the ones the probe of 5b measured on 2026-10-29 and SiteSteps
    // of the test host uses (R212)
    internal sealed class RevitSiteDocument : ISiteDocument
    {
        private readonly Document document;
        private readonly Application application;

        public RevitSiteDocument(Document document, Application application)
        {
            this.document = document;
            this.application = application;
        }

        public SiteState Read()
        {
            Units units = document.GetUnits();
            FormatOptions length = units.GetFormatOptions(SpecTypeId.Length);
            ProjectPosition at = document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            BasePoint survey = BasePoint.GetSurveyPoint(document);
            BasePoint projectBase = BasePoint.GetProjectBasePoint(document);
            var state = new SiteState
            {
                Units = new UnitState
                {
                    System = document.DisplayUnitSystem.ToString(), Length = SiteFacts.UnitName(length.GetUnitTypeId().TypeId), LengthAccuracy = length.Accuracy,
                    Area = UnitOf(units, SpecTypeId.Area), Angle = UnitOf(units, SpecTypeId.Angle), Slope = UnitOf(units, SpecTypeId.Slope),
                },
                Position = new SitePosition
                {
                    EastM = UnitMath.FeetToMetres(at.EastWest), NorthM = UnitMath.FeetToMetres(at.NorthSouth), ElevationM = UnitMath.FeetToMetres(at.Elevation), AngleDeg = UnitMath.RadiansToDegrees(at.Angle),
                },
                Survey = new BasePointState { InternalM = Metres(survey.Position), SharedM = Metres(survey.SharedPosition), Clipped = survey.Clipped },
                ProjectBase = new BasePointState { InternalM = Metres(projectBase.Position), SharedM = Metres(projectBase.SharedPosition), Clipped = projectBase.Clipped },
                Levels = Levels().Select(level => new LevelState
                {
                    Name = level.Name, ElevationM = UnitMath.FeetToMetres(level.Elevation), ProjectElevationM = UnitMath.FeetToMetres(level.ProjectElevation), Base = BaseOf(level),
                }).ToList(),
                FloorTypes = FloorTypes().Select(type => type.Name).ToList(),
                WallTypes = BasicWallTypes().Select(type => type.Name).ToList(),
                SettingsEntity = HasSettings(),
                PositionName = document.ActiveProjectLocation.Name,
                LatitudeDeg = UnitMath.RadiansToDegrees(document.SiteLocation.Latitude),
                LongitudeDeg = UnitMath.RadiansToDegrees(document.SiteLocation.Longitude),
                PlaceName = document.SiteLocation.PlaceName,
            };
            var shared = new HashSet<long>();
            foreach (SharedParameterElement element in new FilteredElementCollector(document).OfClass(typeof(SharedParameterElement)).Cast<SharedParameterElement>())
            {
                shared.Add(element.Id.Value);
                var item = new ParameterState { Name = element.Name, Guid = element.GuidValue.ToString("D") };
                ElementBinding binding = document.ParameterBindings.get_Item(element.GetDefinition()) as ElementBinding;
                if (binding != null)
                {
                    item.Categories = binding.Categories.Cast<Category>().Select(category => category.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
                    item.Group = GroupLabel(element.GetDefinition().GetGroupTypeId());
                }
                state.SharedParameters.Add(item);
            }
            DefinitionBindingMapIterator bound = document.ParameterBindings.ForwardIterator();
            while (bound.MoveNext())
            {
                InternalDefinition definition = bound.Key as InternalDefinition;
                if (definition != null && !shared.Contains(definition.Id.Value))
                {
                    state.ProjectParameters.Add(definition.Name);
                }
            }
            return state;
        }

        // 3.1 Checks, the dry run: the turn by the angle about the internal origin, then the move, made in memory. The probe of 5b found
        // OfPoint of this transform equal to GetTotalTransform().Inverse.OfPoint at 0 and 30 degrees, 0.0000 mm apart
        public double[] SharedOf(SitePosition position, double[] internalM)
        {
            Transform turn = Transform.CreateRotationAtPoint(XYZ.BasisZ, UnitMath.DegreesToRadians(position.AngleDeg.Value), XYZ.Zero);
            turn.Origin = new XYZ(UnitMath.MetresToFeet(position.EastM.Value), UnitMath.MetresToFeet(position.NorthM.Value), UnitMath.MetresToFeet(position.ElevationM.Value));
            return Metres(turn.OfPoint(Feet(internalM)));
        }

        public double[] SharedNow(double[] internalM)
        {
            return Metres(document.ActiveProjectLocation.GetTotalTransform().Inverse.OfPoint(Feet(internalM)));
        }

        // SetFormatOptions changes the copy GetUnits gives, so the copy is written back by SetUnits (03a step 1)
        public void SetUnits(UnitState units)
        {
            Units copy = document.GetUnits();
            FormatOptions length = copy.GetFormatOptions(SpecTypeId.Length);
            length.UseDefault = false;
            length.SetUnitTypeId(UnitId(units.Length));
            length.Accuracy = units.LengthAccuracy.Value;
            copy.SetFormatOptions(SpecTypeId.Length, length);
            SetUnit(copy, SpecTypeId.Area, units.Area);
            SetUnit(copy, SpecTypeId.Angle, units.Angle);
            SetUnit(copy, SpecTypeId.Slope, units.Slope);
            document.SetUnits(copy);
        }

        // SetProjectPosition keeps the internal positions of both points and moves their shared points (probe of 5b). The survey point
        // unclipped moves on its own, not the shared coordinates, and its clip state is set back whatever happens
        public void SetPosition(SetUpTarget target, SetUpChanges changes)
        {
            if (changes.Position)
            {
                SitePosition at = target.Position;
                document.ActiveProjectLocation.SetProjectPosition(XYZ.Zero, new ProjectPosition(UnitMath.MetresToFeet(at.EastM.Value), UnitMath.MetresToFeet(at.NorthM.Value), UnitMath.MetresToFeet(at.ElevationM.Value), UnitMath.DegreesToRadians(at.AngleDeg.Value)));
                document.Regenerate();
            }
            if (changes.SurveyPoint)
            {
                BasePoint survey = BasePoint.GetSurveyPoint(document);
                bool clipped = survey.Clipped;
                try
                {
                    survey.Clipped = false;
                    ElementTransformUtils.MoveElement(document, survey.Id, Feet(target.SurveyInternal) - survey.Position);
                    document.Regenerate();
                }
                finally
                {
                    BasePoint.GetSurveyPoint(document).Clipped = clipped;
                }
            }
            if (changes.BasePoint)
            {
                BasePoint projectBase = BasePoint.GetProjectBasePoint(document);
                ElementTransformUtils.MoveElement(document, projectBase.Id, Feet(target.BaseInternal) - projectBase.Position);
                document.Regenerate();
            }
        }

        // R455. The named position first, so SetProjectPosition of SetPosition sets the position of that name
        public void SetLocation(SetUpTarget target)
        {
            SetNamedPosition(target.Cad.PositionName);
            SetSiteLocation(target.Cad.LatitudeDeg, target.Cad.LongitudeDeg, target.Cad.PlaceName);
        }

        // A position of the name made active, else the active one renamed, so a new project keeps one position and no unused
        // Internal beside it (CHOSEN, Q328)
        private void SetNamedPosition(string name)
        {
            ProjectLocation named = document.ProjectLocations.Cast<ProjectLocation>().FirstOrDefault(location => location.Name == name);
            if (named == null)
            {
                document.ActiveProjectLocation.Name = name;
            }
            else if (named.Id != document.ActiveProjectLocation.Id)
            {
                document.ActiveProjectLocation = named;
            }
            document.Regenerate();
        }

        // SiteLocation takes radians. A place name null leaves the one of the document
        private void SetSiteLocation(double latitudeDeg, double longitudeDeg, string placeName)
        {
            SiteLocation site = document.SiteLocation;
            site.Latitude = UnitMath.DegreesToRadians(latitudeDeg);
            site.Longitude = UnitMath.DegreesToRadians(longitudeDeg);
            if (placeName != null)
            {
                site.PlaceName = placeName;
            }
        }

        // Level.Elevation is measured from the Elevation Base of the level type, so the base is set first (03a step 3, probe of 5b)
        public void SetLevels(SetUpTarget target, SetUpChanges changes)
        {
            int levelBase = LevelBases.Value(target.LevelBase) ?? throw new InvalidOperationException("the position file names the level base " + target.LevelBase + ", which is no Elevation Base of Revit");
            if (changes.LevelsToMake.Count > 0)
            {
                ElementId planType = new FilteredElementCollector(document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(type => type.ViewFamily == ViewFamily.FloorPlan).Id;
                foreach (string name in changes.LevelsToMake)
                {
                    Level made = Level.Create(document, 0);
                    made.Name = name;
                    ViewPlan.Create(document, planType, made.Id);
                }
            }
            List<Level> levels = target.Levels.Select(level => Levels().First(have => have.Name == level.Name)).ToList();
            if (changes.LevelBase)
            {
                foreach (ElementId typeId in levels.Select(level => level.GetTypeId()).Distinct())
                {
                    Parameter parameter = document.GetElement(typeId).get_Parameter(BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE);
                    if (parameter.AsInteger() != levelBase && !parameter.Set(levelBase))
                    {
                        throw new InvalidOperationException("Revit did not set the Elevation Base of the level type " + document.GetElement(typeId).Name);
                    }
                }
                document.Regenerate();
            }
            foreach (PositionLevel level in changes.Levels)
            {
                levels.First(have => have.Name == level.Name).Elevation = UnitMath.MetresToFeet(level.ElevationM);
            }
            document.Regenerate();
        }

        // By name as copies, the generic type first so the other floor types copy it, as R212 does
        public void MakeTypes(SetUpTarget target, SetUpChanges changes)
        {
            if (changes.FloorTypes.Count > 0)
            {
                List<FloorType> floors = FloorTypes();
                FloorType source = floors.FirstOrDefault(type => target.GenericTypes.Contains(type.Name)) ?? floors.First();
                foreach (string name in changes.FloorTypes.OrderBy(name => target.GenericTypes.Contains(name) ? 0 : 1))
                {
                    FloorType made = (FloorType)source.Duplicate(name);
                    if (target.GenericTypes.Contains(name) && !target.GenericTypes.Contains(source.Name))
                    {
                        source = made;
                    }
                }
            }
            if (changes.WallTypes.Count > 0)
            {
                WallType source = BasicWallTypes().First();
                foreach (string name in changes.WallTypes)
                {
                    source.Duplicate(name);
                }
            }
        }

        // Each definition is taken by its GUID from the file of UseParameterFile and bound as an instance parameter, a binding already
        // there by that GUID put again with the categories it had and those of the sheet, under the group of the sheet (R464)
        public void BindParameters(SetUpTarget target, IReadOnlyList<ParameterSpec> parameters)
        {
            Dictionary<string, ForgeTypeId> groups = ParameterUtils.GetAllBuiltInGroups().GroupBy(id => LabelUtils.GetLabelForGroup(id), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(found => found.Key, found => found.First(), StringComparer.OrdinalIgnoreCase);
            DefinitionFile file = application.OpenSharedParameterFile() ?? throw new InvalidOperationException("Revit opened no shared parameter file");
            DefinitionGroup group = file.Groups.get_Item(target.ParameterGroup) ?? throw new InvalidOperationException("the shared parameter file holds no group " + target.ParameterGroup);
            foreach (ParameterSpec item in parameters)
            {
                Guid guid = Guid.Parse(item.Guid);
                ExternalDefinition definition = group.Definitions.Cast<Definition>().OfType<ExternalDefinition>().FirstOrDefault(found => found.GUID == guid)
                    ?? throw new InvalidOperationException("the shared parameter file holds no " + item.Name + " of GUID " + item.Guid);
                CategorySet categories = application.Create.NewCategorySet();
                SharedParameterElement element = SharedParameterElement.Lookup(document, guid);
                ElementBinding had = element == null ? null : document.ParameterBindings.get_Item(element.GetDefinition()) as ElementBinding;
                foreach (Category category in had == null ? new Category[0] : had.Categories.Cast<Category>())
                {
                    categories.Insert(category);
                }
                foreach (string name in item.Categories)
                {
                    categories.Insert(document.Settings.Categories.get_Item(name) ?? throw new InvalidOperationException("the project holds no category " + name + " to bind " + item.Name + " on"));
                }
                InstanceBinding binding = application.Create.NewInstanceBinding(categories);
                ForgeTypeId under = item.Group != null && groups.TryGetValue(item.Group, out ForgeTypeId found) ? found
                    : throw new InvalidOperationException("Revit has no parameter group " + (item.Group ?? "blank") + " to bind " + item.Name + " under, the Group of the parameter sheet (R464)");
                bool done = had == null ? document.ParameterBindings.Insert(definition, binding, under) : document.ParameterBindings.ReInsert(definition, binding, under);
                if (!done)
                {
                    throw new InvalidOperationException("Revit did not bind the parameter " + item.Name);
                }
            }
        }

        // A group of no label, such as the empty group, gives null, so it is never taken for a group the sheet names
        private static string GroupLabel(ForgeTypeId group)
        {
            if (group == null || string.IsNullOrEmpty(group.TypeId))
            {
                return null;
            }
            try
            {
                return LabelUtils.GetLabelForGroup(group);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return null;
            }
        }

        public IDisposable UseParameterFile(string path)
        {
            string before = application.SharedParametersFilename;
            application.SharedParametersFilename = path;
            if (application.OpenSharedParameterFile() == null)
            {
                application.SharedParametersFilename = before;
                throw new InvalidOperationException("Revit did not open the shared parameter file " + path);
            }
            return new Restore(() => application.SharedParametersFilename = before);
        }

        private bool HasSettings()
        {
            ProjectInfo information = document.ProjectInformation;
            return information != null && Schema.ListSchemas().Where(schema => schema.SchemaName == SetUpRun.SettingsSchema).Any(schema => information.GetEntity(schema).IsValid());
        }

        private string BaseOf(Level level)
        {
            Parameter parameter = document.GetElement(level.GetTypeId())?.get_Parameter(BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE);
            return parameter == null ? null : LevelBases.Of(parameter.AsInteger());
        }

        private List<Level> Levels()
        {
            return new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().ToList();
        }

        private List<FloorType> FloorTypes()
        {
            return new FilteredElementCollector(document).OfClass(typeof(FloorType)).Cast<FloorType>().Where(type => !type.IsFoundationSlab).ToList();
        }

        private List<WallType> BasicWallTypes()
        {
            return new FilteredElementCollector(document).OfClass(typeof(WallType)).Cast<WallType>().Where(type => type.Kind == WallKind.Basic).ToList();
        }

        private static string UnitOf(Units units, ForgeTypeId spec)
        {
            return SiteFacts.UnitName(units.GetFormatOptions(spec).GetUnitTypeId().TypeId);
        }

        private static void SetUnit(Units copy, ForgeTypeId spec, string unit)
        {
            FormatOptions options = copy.GetFormatOptions(spec);
            if (SiteFacts.UnitName(options.GetUnitTypeId().TypeId) == unit)
            {
                return;
            }
            options.UseDefault = false;
            options.SetUnitTypeId(UnitId(unit));
            copy.SetFormatOptions(spec, options);
        }

        // The unit of a name as SiteFacts.UnitName gives it from the type id, such as meters
        private static ForgeTypeId UnitId(string name)
        {
            return UnitUtils.GetAllUnits().FirstOrDefault(id => SiteFacts.UnitName(id.TypeId) == name) ?? throw new InvalidOperationException("Revit has no unit " + name);
        }

        private static XYZ Feet(double[] metres)
        {
            return new XYZ(UnitMath.MetresToFeet(metres[0]), UnitMath.MetresToFeet(metres[1]), UnitMath.MetresToFeet(metres[2]));
        }

        private static double[] Metres(XYZ point)
        {
            return new[] { UnitMath.FeetToMetres(point.X), UnitMath.FeetToMetres(point.Y), UnitMath.FeetToMetres(point.Z) };
        }

        private sealed class Restore : IDisposable
        {
            private Action end;

            public Restore(Action end)
            {
                this.end = end;
            }

            public void Dispose()
            {
                Action once = end;
                end = null;
                once?.Invoke();
            }
        }
    }
}
