using System;
using System.Drawing;
using System.Windows.Forms;
namespace UnturnedModManager {
 public static class Dialogs {
  public static DialogResult Show(string text,string caption,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None){return Show(Form.ActiveForm,text,caption,buttons,icon);}
  public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None){using(var dialog=Create(text,caption,buttons,icon))return dialog.ShowDialog(owner);}
  internal static Form Create(string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon){
   var form=new Form{Text=caption,Size=new Size(740,480),MinimumSize=new Size(600,360),StartPosition=FormStartPosition.CenterParent,Font=new Font("Microsoft JhengHei UI",10),BackColor=Color.FromArgb(23,30,42),ForeColor=Color.FromArgb(231,238,249),MinimizeBox=false,MaximizeBox=false,AutoScaleMode=AutoScaleMode.Dpi};
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(20)};layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,55));
   var body=new TextBox{Text=(icon==MessageBoxIcon.Warning?L.Get("Warning")+Environment.NewLine+Environment.NewLine:"")+text,Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=form.BackColor,ForeColor=form.ForeColor};layout.Controls.Add(body,0,0);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
   bool yesNo=buttons==MessageBoxButtons.YesNo;var reject=Make(L.Get(yesNo?"No":"Text090"),yesNo?DialogResult.No:DialogResult.OK);actions.Controls.Add(reject);form.CancelButton=reject;form.AcceptButton=reject;
   if(yesNo){var confirm=Make(L.Get("Yes"),DialogResult.Yes);confirm.BackColor=Color.FromArgb(26,110,170);actions.Controls.Add(confirm);}
   layout.Controls.Add(actions,0,1);form.Controls.Add(layout);form.Shown+=delegate{body.SelectionStart=0;body.SelectionLength=0;reject.Focus();};return form;
  }
  static Button Make(string text,DialogResult result){var button=new Button{Text=text,DialogResult=result,AutoSize=true,MinimumSize=new Size(100,38),FlatStyle=FlatStyle.Flat,Margin=new Padding(6,10,0,0),BackColor=Color.FromArgb(39,53,72),ForeColor=Color.White};button.FlatAppearance.BorderColor=Color.FromArgb(66,84,108);return button;}
 }
}
