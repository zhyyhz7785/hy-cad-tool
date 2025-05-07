//using system;
//using system.collections.generic;
//using system.linq;
//using autodesk.autocad.applicationservices;
//using autodesk.autocad.databaseservices;
//using autodesk.autocad.editorinput;
//using autodesk.autocad.runtime;
//namespace HyCADTool
//{
//    public static partial class testfunction
//    {
//        private static list<string> storedvalues = new list<string>();
//        // commandmethod 特性表明这是一个 autocad 命令
//        [commandmethod("teb")]
//        public static void selectnouse()
//        {
//            document doc = application.documentmanager.mdiactivedocument;
//            editor ed = doc.editor;
//            // 获取用户输入的固定数值
//            list<string> fixedvalues = getuserinputvalues(ed);
//            if (fixedvalues == null) return;
//            // 存储用户输入的值
//            storedvalues.addrange(fixedvalues.except(storedvalues));
//            // 获取用户选择的图形对象
//            selectionset selset = getuserselection(ed);
//            if (selset == null) return;
//            // 筛选出匹配条件的文字对象，并包括目标图层的所有图形
//            list<objectid> matchingtextobjects = getmatchingtextobjects(selset, fixedvalues, "柱墩板元配筋标注");
//            // 包括符合条件的图层中的所有实体
//            addentitiesfromlayers(matchingtextobjects, "等值线高度1", "等值线标注");
//            // 高亮显示匹配的图形对象
//            highlightmatchingobjects(ed, matchingtextobjects);
//        }
//        private static list<string> getuserinputvalues(editor ed)
//        {
//            promptstringoptions promptoptions = new promptstringoptions("\n请输入要筛选的固定数值（使用空格、逗号或空格+逗号分隔），或按enter使用上次输入的数据: ");
//            promptoptions.allowspaces = true;
//            promptresult promptresult = ed.getstring(promptoptions);
//            if (promptresult.status != promptstatus.ok)
//            {
//                ed.writemessage("\n用户取消了操作.");
//                return null;
//            }
//            string inputvalues = promptresult.stringresult;
//            if (string.isnullorwhitespace(inputvalues))
//            {
//                if (storedvalues.count == 0)
//                {
//                    ed.writemessage("\n没有存储的值，请输入固定数值.");
//                    return null;
//                }
//                return storedvalues;
//            }
//            char[] delimiters = new char[] { ' ', ',', '，' };
//            list<string> fixedvalues = inputvalues.split(delimiters, stringsplitoptions.removeemptyentries).tolist();
//            return fixedvalues;
//        }
//        private static selectionset getuserselection(editor ed)
//        {
//            promptselectionoptions selopts = new promptselectionoptions();
//            selopts.messageforadding = "\n请选择图形对象: ";
//            promptselectionresult selres = ed.getselection(selopts);
//            if (selres.status != promptstatus.ok)
//            {
//                ed.writemessage("\n用户取消了选择.");
//                return null;
//            }
//            return selres.value;
//        }
//        private static list<objectid> getmatchingtextobjects(selectionset selset, list<string> fixedvalues, string targetlayername)
//        {
//            list<objectid> matchingtextobjects = new list<objectid>();
//            document doc = application.documentmanager.mdiactivedocument;
//            database db = doc.database;
//            using (transaction trans = db.transactionmanager.starttransaction())
//            {
//                foreach (selectedobject selobj in selset)
//                {
//                    if (selobj != null)
//                    {
//                        dbobject dbobj = trans.getobject(selobj.objectid, openmode.forread);
//                        if (dbobj is dbtext dbtext && matchescondition(dbtext.textstring, fixedvalues))
//                        {
//                            matchingtextobjects.add(selobj.objectid);
//                        }
//                        else if (dbobj is mtext mtext && matchescondition(mtext.text, fixedvalues))
//                        {
//                            matchingtextobjects.add(selobj.objectid);
//                        }
//                    }
//                }
//                // 包括目标图层的所有图形
//                blocktable bt = (blocktable)trans.getobject(db.blocktableid, openmode.forread);
//                blocktablerecord btr = (blocktablerecord)trans.getobject(bt[blocktablerecord.modelspace], openmode.forread);
//                foreach (objectid objid in btr)
//                {
//                    dbobject obj = trans.getobject(objid, openmode.forread);
//                    if (obj is entity entity && entity.layer == targetlayername)
//                    {
//                        matchingtextobjects.add(objid);
//                    }
//                }
//                trans.commit();
//            }
//            return matchingtextobjects;
//        }
//        private static void addentitiesfromlayers(list<objectid> matchingtextobjects, string layernameprefix, string additionallayername)
//        {
//            document doc = application.documentmanager.mdiactivedocument;
//            database db = doc.database;
//            using (transaction trans = db.transactionmanager.starttransaction())
//            {
//                blocktable bt = (blocktable)trans.getobject(db.blocktableid, openmode.forread);
//                blocktablerecord btr = (blocktablerecord)trans.getobject(bt[blocktablerecord.modelspace], openmode.forread);
//                foreach (objectid objid in btr)
//                {
//                    dbobject obj = trans.getobject(objid, openmode.forread);
//                    if (obj is entity entity && (entity.layer.startswith(layernameprefix) || entity.layer == additionallayername))
//                    {
//                        matchingtextobjects.add(objid);
//                    }
//                }
//                trans.commit();
//            }
//        }
//        private static void highlightmatchingobjects(editor ed, list<objectid> matchingtextobjects)
//        {
//            if (matchingtextobjects.count > 0)
//            {
//                ed.setimpliedselection(matchingtextobjects.toarray());
//                ed.writemessage($"\n共找到 {matchingtextobjects.count} 个匹配的图形对象.");
//            }
//            else
//            {
//                ed.writemessage("\n未找到匹配的图形对象.");
//            }
//        }
//        private static bool matchescondition(string text, list<string> conditions)
//        {
//            foreach (var condition in conditions)
//            {
//                if (double.tryparse(text, out double textvalue))
//                {
//                    if (condition.startswith(">") && double.tryparse(condition.substring(1), out double greaterthanvalue))
//                    {
//                        if (textvalue > greaterthanvalue) return true;
//                    }
//                    else if (condition.startswith("<") && double.tryparse(condition.substring(1), out double lessthanvalue))
//                    {
//                        if (textvalue < lessthanvalue) return true;
//                    }
//                    else if (double.tryparse(condition, out double exactvalue))
//                    {
//                        if (textvalue == exactvalue) return true;
//                    }
//                }
//            }
//            return false;
//        }
//    }
//}
