using System;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace UnturnedModManager {
 public partial class MainForm {
  bool fittingBatch;Func<string,string> assistantStateResolver=Engine.DefaultAssistantState;
  void BuildReconciliationExperience(){
   InstallDirectChecks(grid);versionGrid.EditMode=DataGridViewEditMode.EditProgrammatically;InstallDirectChecks(versionGrid);

   var footer=(TableLayoutPanel)status.Parent;
   footer.Controls.Remove(status);footer.Controls.Remove(progress);
   footer.Margin=new Padding(WorkspaceGap/2,0,WorkspaceGap/2,0);
   footer.ColumnCount=1;footer.RowCount=2;footer.ColumnStyles.Clear();
   footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.RowStyles.Clear();
   footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   footer.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
   status.Margin=Padding.Empty;progress.Margin=new Padding(0,0,0,WorkspaceGap/2);
   footer.Controls.Add(status,0,0);footer.Controls.Add(progress,0,1);
   FitProgressFooter();
   // Keep commands visible and let the flow layout wrap at narrow widths.
   var commandHost=batchBar.Parent;var commandSkin=commandHost.Parent;commandHost.Controls.Remove(batchBar);workspaceRoot.Controls.Remove(commandSkin);commandSkin.Dispose();var commands=(FlowLayoutPanel)batchBar;
   // ScrollSurface clips its child with a native window region. The command bar
   // now lives directly in the layout, so release that obsolete clipping region.
   var obsoleteRegion=commands.Region;commands.Region=null;if(obsoleteRegion!=null)obsoleteRegion.Dispose();
   commands.WrapContents=true;commands.AutoScroll=false;commands.Margin=Padding.Empty;commands.Padding=Padding.Empty;commands.Dock=DockStyle.Fill;workspaceRoot.Controls.Add(commands,0,4);commands.Layout+=delegate{FitBatchActions();};
   navigation.Padding=new Padding(4,8,4,4);navToggle.Height=44;UpdateNavigation();CaptureKeys(this);Theme(this);UpdateNavigation();UpdateSelection();
  }
  void FitBatchActions(){if(fittingBatch||batchBar==null||workspaceRoot==null||tabs.SelectedIndex!=0)return;fittingBatch=true;try{int width=Math.Max(1,batchBar.ClientSize.Width-batchBar.Padding.Horizontal),x=0,line=0,total=batchBar.Padding.Vertical;foreach(Control action in batchBar.Controls){if(!action.Visible)continue;int w=action.Width+action.Margin.Horizontal,h=action.Height+action.Margin.Vertical;if(x>0&&x+w>width){total+=line;x=0;line=0;}x+=w;line=Math.Max(line,h);}float height=total+line;if(workspaceRoot.RowStyles[4].Height!=height){workspaceRoot.RowStyles[4].Height=height;if(IsHandleCreated)BeginInvoke(new Action(delegate{if(!IsDisposed)workspaceRoot.PerformLayout();}));}}finally{fittingBatch=false;}FitNavigation();}
  void FitProgressFooter(){
   var footer=status.Parent as TableLayoutPanel;
   if(footer==null||footer.RowCount!=2||workspaceRoot==null)return;
   var shell=(TableLayoutPanel)footer.Parent;
   int progressHeight=busy?status.Font.Height/2+WorkspaceGap:0;
   int height=status.Font.Height+WorkspaceGap+progressHeight;
   progress.Visible=busy;
   jobSummary.Visible=progressExpanded;
   footer.RowStyles[1].Height=progressHeight;
   shell.RowStyles[0].Height=height;
   workspaceRoot.RowStyles[5].Height=height+(progressExpanded?status.Font.Height*4+WorkspaceGap*2:0);
  }
  void InstallDirectChecks(DataGridView view){((SmoothGrid)view).DirectCheck=index=>ToggleDirectCheck(view,index);}
  void ToggleDirectCheck(DataGridView view,int index){if(busy||index<0||index>=view.RowCount||view.Rows[index].Cells[0].ReadOnly||!view.Rows[index].Visible)return;var cell=view.Rows[index].Cells[0];if(view==grid)cell.Value=!Convert.ToBoolean(cell.Value);else{var parent=view.Rows[index].Tag as Row;var option=view.Rows[index].Tag as VersionOption;bool all=parent!=null?parent.Options.All(o=>checkedVersions.Contains(OptionId(o))):option!=null&&checkedVersions.Contains(OptionId(option));renderingVersions=true;try{if(parent!=null)foreach(var child in parent.Options)SetVersionChecked(child,!all);else if(option!=null)SetVersionChecked(option,!all);}finally{renderingVersions=false;}BeginInvoke(new Action(RefreshVersions));}view.InvalidateColumn(0);}
 }
}
