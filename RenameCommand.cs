using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace XdataAutoUpdate
{
    public static class CivilPartRenamer
    {
        private const int MaxRecordedErrors = 50;

        public static int PressurePipeRenamed { get; private set; }
        public static int PressurePipeFailed { get; private set; }
        public static int PressureFittingRenamed { get; private set; }
        public static int PressureFittingFailed { get; private set; }
        public static int PipeRenamed { get; private set; }
        public static int PipeFailed { get; private set; }

        public static readonly List<string> Errors = new List<string>();

        public static void Reset()
        {
            PressurePipeRenamed = 0;
            PressurePipeFailed = 0;
            PressureFittingRenamed = 0;
            PressureFittingFailed = 0;
            PipeRenamed = 0;
            PipeFailed = 0;
            Errors.Clear();
        }

        public static int RenamePressureParts(Transaction tr, CivilDocument civilDoc)
        {
            int pipeRenamed = 0;
            int pipeFailed = 0;
            int fittingRenamed = 0;
            int fittingFailed = 0;

            ObjectIdCollection networkIds = civilDoc.GetPressurePipeNetworkIds();
            if (networkIds != null)
            {
                foreach (ObjectId networkId in networkIds)
                {
                    if (networkId.IsNull || !networkId.IsValid)
                    {
                        continue;
                    }

                    PressurePipeNetwork network =
                        tr.GetObject(networkId, OpenMode.ForRead) as PressurePipeNetwork;
                    if (network == null)
                    {
                        continue;
                    }

                    RenamePressurePartsById(
                        tr, network.GetPipeIds(), "CON", ref pipeRenamed, ref pipeFailed);
                    RenamePressurePartsById(
                        tr, network.GetFittingIds(), "RAC", ref fittingRenamed, ref fittingFailed);
                }
            }

            PressurePipeRenamed = pipeRenamed;
            PressurePipeFailed = pipeFailed;
            PressureFittingRenamed = fittingRenamed;
            PressureFittingFailed = fittingFailed;

            return pipeRenamed + fittingRenamed;
        }

        public static int RenamePipeParts(Transaction tr, CivilDocument civilDoc)
        {
            int renamed = 0;
            int failed = 0;

            ObjectIdCollection networkIds = civilDoc.GetPipeNetworkIds();
            if (networkIds != null)
            {
                foreach (ObjectId networkId in networkIds)
                {
                    if (networkId.IsNull || !networkId.IsValid)
                    {
                        continue;
                    }

                    Network network = tr.GetObject(networkId, OpenMode.ForRead) as Network;
                    if (network == null)
                    {
                        continue;
                    }

                    string networkName = network.Name;
                    if (string.IsNullOrWhiteSpace(networkName))
                    {
                        continue;
                    }

                    RenameGravityPartsById(
                        tr, network.GetPipeIds(), networkName, ref renamed, ref failed);
                }
            }

            PipeRenamed = renamed;
            PipeFailed = failed;

            return renamed;
        }

        private static void RenamePressurePartsById(
            Transaction tr,
            ObjectIdCollection partIds,
            string type,
            ref int renamed,
            ref int failed)
        {
            if (partIds == null)
            {
                return;
            }

            foreach (ObjectId partId in partIds)
            {
                if (partId.IsNull || !partId.IsValid)
                {
                    continue;
                }

                PressurePart part;
                try
                {
                    part = tr.GetObject(partId, OpenMode.ForWrite) as PressurePart;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    failed++;
                    RecordError(type + " " + partId + ": " + ex.Message);
                    continue;
                }

                if (part == null)
                {
                    continue;
                }

                RenameOpenedPart(part, part.NetworkName, type, ref renamed, ref failed);
            }
        }

        private static void RenameGravityPartsById(
            Transaction tr,
            ObjectIdCollection partIds,
            string networkName,
            ref int renamed,
            ref int failed)
        {
            if (partIds == null)
            {
                return;
            }

            foreach (ObjectId partId in partIds)
            {
                if (partId.IsNull || !partId.IsValid)
                {
                    continue;
                }

                Autodesk.AutoCAD.DatabaseServices.DBObject part;
                try
                {
                    part = tr.GetObject(partId, OpenMode.ForWrite);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    failed++;
                    RecordError("CON " + partId + ": " + ex.Message);
                    continue;
                }

                if (part == null)
                {
                    continue;
                }

                RenameOpenedPart(part, networkName, "CON", ref renamed, ref failed);
            }
        }

        private static void RenameOpenedPart(
            Autodesk.AutoCAD.DatabaseServices.DBObject part,
            string networkName,
            string type,
            ref int renamed,
            ref int failed)
        {
            if (string.IsNullOrWhiteSpace(networkName))
            {
                return;
            }

            string handle;
            try
            {
                handle = part.Handle.ToString();
            }
            catch (System.Exception ex)
            {
                failed++;
                RecordError(type + " ?: " + ex.Message);
                return;
            }

            string newName = networkName + "-" + type + "-" + handle;

            try
            {
                dynamic comPart = part.AcadObject;
                comPart.Name = newName;
                renamed++;
            }
            catch (System.Exception ex)
            {
                failed++;
                RecordError(type + " " + handle + ": " + ex.Message);
            }
        }

        private static void RecordError(string message)
        {
            if (Errors.Count < MaxRecordedErrors)
            {
                Errors.Add(message);
            }
        }
    }

#if MANUAL
    public class RenameCommand
    {
        [CommandMethod("C3D_PN_RENAME")]
        public void RenameCivilParts()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;
            CivilDocument civilDoc = CivilApplication.ActiveDocument;
            if (civilDoc == null)
            {
                ed.WriteMessage("\nC3D_PN_RENAME: no active Civil 3D document.");
                return;
            }

            CivilPartRenamer.Reset();

            try
            {
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        CivilPartRenamer.RenamePressureParts(tr, civilDoc);
                        CivilPartRenamer.RenamePipeParts(tr, civilDoc);
                        tr.Commit();
                    }
                    catch
                    {
                        tr.Abort();
                        throw;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nC3D_PN_RENAME failed: " + ex.Message);
                return;
            }

            ed.WriteMessage(
                "\nC3D_PN_RENAME:"
                + "\n  PressurePipe: {0} renommees, {1} echecs"
                + "\n  PressureFitting: {2} renommees, {3} echecs"
                + "\n  Pipe: {4} renommees, {5} echecs",
                CivilPartRenamer.PressurePipeRenamed,
                CivilPartRenamer.PressurePipeFailed,
                CivilPartRenamer.PressureFittingRenamed,
                CivilPartRenamer.PressureFittingFailed,
                CivilPartRenamer.PipeRenamed,
                CivilPartRenamer.PipeFailed);

            int shown = 0;
            foreach (string err in CivilPartRenamer.Errors)
            {
                if (shown >= 10)
                {
                    break;
                }

                ed.WriteMessage("\n  ! " + err);
                shown++;
            }
        }
    }
#endif
}
