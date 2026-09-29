#if AUTO
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;

namespace XdataAutoUpdate
{
    public class AutoUpdateReactor
    {
        private static bool _enabled;
        private static bool _running;
        private static Database _attachedDb;

        [CommandMethod("C3D_PN_XDATA_AUTO_ON")]
        public static void Enable()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            if (_enabled)
            {
                doc.Editor.WriteMessage("\nC3D_PN_XDATA_AUTO is already enabled.");
                return;
            }

            EnableInternal(doc);
            doc.Editor.WriteMessage("\nC3D_PN_XDATA_AUTO enabled (XData written on save).");
        }

        [CommandMethod("C3D_PN_XDATA_AUTO_OFF")]
        public static void Disable()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc != null ? doc.Editor : null;

            if (!_enabled)
            {
                if (ed != null)
                {
                    ed.WriteMessage("\nC3D_PN_XDATA_AUTO is not enabled.");
                }

                return;
            }

            DisableInternal();

            if (ed != null)
            {
                ed.WriteMessage("\nC3D_PN_XDATA_AUTO disabled.");
            }
        }

        [CommandMethod("C3D_PN_XDATA_AUTO")]
        public static void RunNow()
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
                ed.WriteMessage("\nC3D_PN_XDATA_AUTO: no active Civil 3D document.");
                return;
            }

            try
            {
                int partCount = XdataWriter.WriteAll(doc.Database, civilDoc);
                ed.WriteMessage(
                    "\nC3D_PN_XDATA_AUTO: updated XData on {0} part(s).",
                    partCount);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nC3D_PN_XDATA_AUTO failed: " + ex.Message);
            }
        }

        internal static void EnableInternal(Document doc)
        {
            if (doc == null)
            {
                return;
            }

            EnsureRegApp(doc.Database);

            if (!_enabled)
            {
                Application.DocumentManager.DocumentActivated += OnDocumentActivated;
                _enabled = true;
            }

            Attach(doc.Database);
        }

        internal static void DisableInternal()
        {
            Application.DocumentManager.DocumentActivated -= OnDocumentActivated;
            Detach();
            _enabled = false;
        }

        private static void EnsureRegApp(Database db)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    XdataWriter.EnsureRegApp(tr, db);
                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
            }
        }

        private static void Attach(Database db)
        {
            if (db == null || db == _attachedDb)
            {
                return;
            }

            Detach();
            db.BeginSave += OnBeginSave;
            _attachedDb = db;
        }

        private static void Detach()
        {
            if (_attachedDb == null)
            {
                return;
            }

            try
            {
                _attachedDb.BeginSave -= OnBeginSave;
            }
            catch (System.Exception)
            {
            }

            _attachedDb = null;
        }

        private static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (!_enabled || e.Document == null)
            {
                return;
            }

            Attach(e.Document.Database);
        }

        private static void OnBeginSave(object sender, DatabaseIOEventArgs e)
        {
            if (_running)
            {
                return;
            }

            Database db = sender as Database;
            if (db == null)
            {
                return;
            }

            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || doc.Database != db)
            {
                return;
            }

            CivilDocument civilDoc = CivilApplication.ActiveDocument;
            if (civilDoc == null)
            {
                return;
            }

            _running = true;
            try
            {
                CivilPartRenamer.Reset();

                int xdataCount = 0;
                int renamed = 0;
                int renameFailed = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        xdataCount = XdataWriter.WriteAllInTransaction(tr, db, civilDoc);

                        CivilPartRenamer.RenamePressureParts(tr, civilDoc);
                        CivilPartRenamer.RenamePipeParts(tr, civilDoc);

                        renamed = CivilPartRenamer.PressurePipeRenamed
                            + CivilPartRenamer.PressureFittingRenamed
                            + CivilPartRenamer.PipeRenamed;
                        renameFailed = CivilPartRenamer.PressurePipeFailed
                            + CivilPartRenamer.PressureFittingFailed
                            + CivilPartRenamer.PipeFailed;

                        tr.Commit();
                    }
                    catch
                    {
                        tr.Abort();
                        throw;
                    }
                }

                doc.Editor.WriteMessage(
                    "\nC3D_PN_XDATA_AUTO: {0} XData ecrites, {1} parts renommees, {2} echecs.",
                    xdataCount, renamed, renameFailed);
            }
            catch (System.Exception ex)
            {
                try
                {
                    doc.Editor.WriteMessage(
                        "\nC3D_PN_XDATA_AUTO failed on save: " + ex.Message);
                }
                catch (System.Exception)
                {
                }
            }
            finally
            {
                _running = false;
            }
        }
    }

    public class AutoUpdateExtension : IExtensionApplication
    {
        private static DocumentCollectionEventHandler _firstActivation;

        public void Initialize()
        {
            DocumentCollection docs = Application.DocumentManager;

            if (docs.MdiActiveDocument != null)
            {
                TryEnable(docs.MdiActiveDocument);
                return;
            }

            _firstActivation = OnFirstDocumentActivated;
            docs.DocumentActivated += _firstActivation;
        }

        public void Terminate()
        {
            UnsubscribeFirstActivation();
            AutoUpdateReactor.DisableInternal();
        }

        private static void OnFirstDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null)
            {
                return;
            }

            UnsubscribeFirstActivation();
            TryEnable(e.Document);
        }

        private static void TryEnable(Document doc)
        {
            try
            {
                AutoUpdateReactor.EnableInternal(doc);
            }
            catch (System.Exception ex)
            {
                try
                {
                    doc.Editor.WriteMessage(
                        "\nC3D_PN_XDATA_AUTO could not enable on load: " + ex.Message);
                }
                catch (System.Exception)
                {
                }
            }
        }

        private static void UnsubscribeFirstActivation()
        {
            if (_firstActivation == null)
            {
                return;
            }

            Application.DocumentManager.DocumentActivated -= _firstActivation;
            _firstActivation = null;
        }
    }
}
#endif
