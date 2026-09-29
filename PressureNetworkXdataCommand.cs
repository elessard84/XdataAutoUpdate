using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace XdataAutoUpdate
{
    internal static class XdataWriter
    {
        public const string RegAppName = "CIVIL3D_PN_NAME";

        public static int WriteAll(Database db, CivilDocument civilDoc)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    int count = WriteAllInTransaction(tr, db, civilDoc);
                    tr.Commit();
                    return count;
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        public static int WriteAllInTransaction(
            Transaction tr, Database db, CivilDocument civilDoc)
        {
            EnsureRegApp(tr, db);

            int count = 0;
            count += WritePressureNetworks(tr, civilDoc);
            count += WritePipeNetworks(tr, civilDoc);
            return count;
        }

        public static int WritePressureNetworks(Transaction tr, CivilDocument civilDoc)
        {
            int count = 0;

            ObjectIdCollection networkIds = civilDoc.GetPressurePipeNetworkIds();
            if (networkIds == null)
            {
                return count;
            }

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

                count += WritePressureParts(tr, network.GetPipeIds());
                count += WritePressureParts(tr, network.GetFittingIds());
                count += WritePressureParts(tr, network.GetAppurtenanceIds());
            }

            return count;
        }

        public static int WritePipeNetworks(Transaction tr, CivilDocument civilDoc)
        {
            int count = 0;

            ObjectIdCollection networkIds = civilDoc.GetPipeNetworkIds();
            if (networkIds == null)
            {
                return count;
            }

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

                count += WriteNetworkParts(tr, network.GetPipeIds(), networkName);
                count += WriteNetworkParts(tr, network.GetStructureIds(), networkName);
            }

            return count;
        }

        public static void EnsureRegApp(Transaction tr, Database db)
        {
            RegAppTable regAppTable =
                (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);

            if (regAppTable.Has(RegAppName))
            {
                return;
            }

            regAppTable.UpgradeOpen();

            RegAppTableRecord record = new RegAppTableRecord();
            record.Name = RegAppName;
            regAppTable.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        private static int WritePressureParts(Transaction tr, ObjectIdCollection partIds)
        {
            int count = 0;

            if (partIds == null)
            {
                return count;
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
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }

                if (part == null)
                {
                    continue;
                }

                string networkName = part.NetworkName;
                if (string.IsNullOrWhiteSpace(networkName))
                {
                    continue;
                }

                SetNetworkNameXData(part, networkName);
                count++;
            }

            return count;
        }

        private static int WriteNetworkParts(
            Transaction tr, ObjectIdCollection partIds, string networkName)
        {
            int count = 0;

            if (partIds == null)
            {
                return count;
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
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }

                if (part == null)
                {
                    continue;
                }

                SetNetworkNameXData(part, networkName);
                count++;
            }

            return count;
        }

        private static void SetNetworkNameXData(
            Autodesk.AutoCAD.DatabaseServices.DBObject obj, string networkName)
        {
            ResultBuffer existing = obj.GetXDataForApplication(RegAppName);
            if (existing != null)
            {
                existing.Dispose();
            }

            using (ResultBuffer rb = new ResultBuffer(
                new TypedValue((short)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((short)DxfCode.ExtendedDataAsciiString, networkName)))
            {
                obj.XData = rb;
            }
        }
    }

#if MANUAL
    public class PressureNetworkXdataCommand
    {
        [CommandMethod("C3D_PN_XDATA")]
        public void WritePressureNetworkNameXData()
        {
            Document acDoc = Application.DocumentManager.MdiActiveDocument;
            if (acDoc == null)
            {
                return;
            }

            Editor ed = acDoc.Editor;
            Database db = acDoc.Database;

            CivilDocument civilDoc = CivilApplication.ActiveDocument;
            if (civilDoc == null)
            {
                ed.WriteMessage("\nC3D_PN_XDATA: no active Civil 3D document.");
                return;
            }

            try
            {
                int partCount = XdataWriter.WriteAll(db, civilDoc);
                ed.WriteMessage(
                    "\nC3D_PN_XDATA: updated XData on {0} part(s).",
                    partCount);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nC3D_PN_XDATA failed: " + ex.Message);
            }
        }
    }
#endif
}
