using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.HelpClass;
using HyCADTool.Interfaces;
using HyCADTool.Services;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;


using System.Windows;
using System.Windows.Controls;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
namespace HyCADTool.Views
{
    public partial class FilterPanel : UserControl
    {

        public FilterPanel()
        {
            InitializeComponent();
            DataContext = new ViewModels.FilterPanelViewModel();
        }
    }
}