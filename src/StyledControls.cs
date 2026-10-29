using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
namespace UnturnedModManager {
 public static class MenuLifetime {public static void Release(Control owner,ContextMenuStrip menu){if(owner.IsHandleCreated&&!owner.IsDisposed)owner.BeginInvoke(new Action(delegate{if(!menu.IsDisposed)menu.Dispose();}));}}
 public class StyledComboBox : ComboBox {
  internal ContextMenuStrip OpenChoices;
  public StyledComboBox(){FlatStyle=FlatStyle.Flat;DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=26;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  void Face(Graphics g){var t=Themes.Current;g.Clear(t.Raised);using(var pen=new Pen(Focused?t.Accent:t.Border))g.DrawRectangle(pen,0,0,Width-1,Height-1);TextRenderer.DrawText(g,Text,Font,new Rectangle(10,1,Math.Max(1,Width-40),Height-2),Enabled?t.Text:t.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);using(var pen=new Pen(Enabled?t.Text:t.Muted,1.6f)){int x=Width-18,y=Height/2;g.DrawLines(pen,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});}}
  protected override void OnPaint(PaintEventArgs e){Face(e.Graphics);}
  protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
  void Choices(){if(Items.Count==0)return;var menu=new ContextMenuStrip{Renderer=new ThemeMenuRenderer(),BackColor=Themes.Current.Surface,ForeColor=Themes.Current.Text,ShowImageMargin=false};for(int i=0;i<Items.Count;i++){int index=i;var item=new ToolStripMenuItem(GetItemText(Items[i])){Checked=SelectedIndex==i};item.Click+=delegate{SelectedIndex=index;};menu.Items.Add(item);}OpenChoices=menu;menu.Closed+=delegate{OpenChoices=null;MenuLifetime.Release(this,menu);Invalidate();};menu.Show(this,new Point(0,Height));}
  protected override void WndProc(ref Message m){if(Enabled&&DropDownStyle==ComboBoxStyle.DropDownList&&(m.Msg==0x201||(m.Msg==0x100||m.Msg==0x104)&&(m.WParam.ToInt64()==0x73||m.WParam.ToInt64()==0x20||m.WParam.ToInt64()==0x28&&((ModifierKeys&Keys.Alt)!=0||m.Msg==0x104)))){Focus();Choices();m.Result=IntPtr.Zero;return;}if(m.Msg==0x318&&m.WParam!=IntPtr.Zero){using(var graphics=Graphics.FromHdc(m.WParam))Face(graphics);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 }
 public class InputFrame : Panel {
  public InputFrame(){DoubleBuffered=true;Padding=new Padding(10,6,10,5);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Themes.Current.Background:Parent.BackColor);using(var path=SectionPanel.Round(new Rectangle(1,1,Width-3,Height-3),6)){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(Themes.Current.Raised))e.Graphics.FillPath(brush,path);using(var pen=new Pen(ContainsFocus?Themes.Current.Accent:Themes.Current.Border))e.Graphics.DrawPath(pen,path);}}
 }
 public class StyledTextBox : TextBox {
  public StyledTextBox(){BorderStyle=BorderStyle.None;}
  protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.Style&=~0x300000;return cp;}}
 }
 public class SmoothGrid : DataGridView {
  internal ScrollSurface Skin;
  internal Action<int> DirectCheck;
  int pressedCheck=-1;
  public SmoothGrid(){DoubleBuffered=true;}
  protected override void WndProc(ref Message m){
   // These grids use EditProgrammatically for the themed version picker. Handle
   // checkbox gestures independently so native edit activation cannot swallow them.
   if(Enabled&&DirectCheck!=null){
    if(m.Msg==0x201||m.Msg==0x203){
     var hit=HitTest((short)((long)m.LParam&65535),(short)((long)m.LParam>>16));
     if(hit.ColumnIndex==0&&hit.RowIndex>=0&&!Rows[hit.RowIndex].Cells[0].ReadOnly){
      Focus();CurrentCell=Rows[hit.RowIndex].Cells[0];pressedCheck=hit.RowIndex;
      Capture=true;m.Result=IntPtr.Zero;return;
     }
    }
    if(m.Msg==0x202&&pressedCheck>=0){
     int prior=pressedCheck;pressedCheck=-1;Capture=false;
     var hit=HitTest((short)((long)m.LParam&65535),(short)((long)m.LParam>>16));
     if(hit.ColumnIndex==0&&hit.RowIndex==prior)DirectCheck(prior);
     m.Result=IntPtr.Zero;return;
    }
    if(m.Msg==0x215)pressedCheck=-1; // Lost capture cancels an unfinished click.
    // WParam can hold a 64-bit pointer for unrelated messages. Check the
    // message kind first and compare at pointer width; IntPtr.ToInt32 can throw.
    if((m.Msg==0x100||m.Msg==0x101||m.Msg==0x102)&&
       CurrentCell!=null&&CurrentCell.ColumnIndex==0&&m.WParam.ToInt64()==0x20){
     if(m.Msg==0x101)DirectCheck(CurrentCell.RowIndex);
     m.Result=IntPtr.Zero;return;
    }
   }
   if(m.Msg==0x20A&&Skin!=null){Skin.Wheel((short)((long)m.WParam>>16));m.Result=IntPtr.Zero;return;}
   base.WndProc(ref m);
  }
 }
 public class SectionPanel : Panel {
  public string TitleKey;string title;public string Title{get{return title;}set{title=value;Invalidate();}}
  public SectionPanel(){DoubleBuffered=true;}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Themes.Current.Background:Parent.BackColor);var t=Themes.Current;using(var path=Round(new Rectangle(1,1,Width-3,Height-3),12)){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var b=new SolidBrush(t.Surface))e.Graphics.FillPath(b,path);using(var p=new Pen(t.Border))e.Graphics.DrawPath(p,path);}TextRenderer.DrawText(e.Graphics,Title??"",Font,new Rectangle(15,10,Math.Max(1,Width-30),26),t.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
  internal static GraphicsPath Round(Rectangle box,int radius){var p=new GraphicsPath();if(box.Width<radius*2||box.Height<radius*2){p.AddRectangle(box);return p;}p.AddArc(box.Left,box.Top,radius*2,radius*2,180,90);p.AddArc(box.Right-radius*2,box.Top,radius*2,radius*2,270,90);p.AddArc(box.Right-radius*2,box.Bottom-radius*2,radius*2,radius*2,0,90);p.AddArc(box.Left,box.Bottom-radius*2,radius*2,radius*2,90,90);p.CloseFigure();return p;}
 }
 public class SlimScrollBar : Control {
  public int Total,Page,Position;public bool Horizontal;public event Action<int> Change;bool dragging;int grab;
  public SlimScrollBar(){Width=14;Height=14;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=false;}
  int Length{get{return Horizontal?Width:Height;}}
  Rectangle Thumb{get{int length=Length;int size=Math.Min(length,Math.Max(28,Total<=0?length:length*Math.Max(1,Page)/Math.Max(1,Total)));int offset=(length-size)*Position/Math.Max(1,Total-Page);return Horizontal?new Rectangle(offset+2,3,Math.Max(1,size-4),Math.Max(1,Height-6)):new Rectangle(3,offset+2,Math.Max(1,Width-6),Math.Max(1,size-4));}}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Themes.Current.Surface);if(Total<=Page)return;using(var path=SectionPanel.Round(Thumb,3))using(var b=new SolidBrush(dragging?Themes.Current.Accent:Themes.Current.Muted)){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.FillPath(b,path);}}
  void MoveTo(int value){Position=Math.Max(0,Math.Min(Math.Max(0,Total-Page),value));if(Change!=null)Change(Position);Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);int point=Horizontal?e.X:e.Y;var thumb=Thumb;if(thumb.Contains(e.Location)){dragging=true;grab=point-(Horizontal?thumb.X:thumb.Y);Capture=true;}else MoveTo(Position+(point<(Horizontal?thumb.X:thumb.Y)?-Page:Page));}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;int size=Horizontal?Thumb.Width:Thumb.Height;int point=(Horizontal?e.X:e.Y)-grab;MoveTo((int)((long)Math.Max(0,point)*Math.Max(0,Total-Page)/Math.Max(1,Length-size)));}
  protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnMouseWheel(MouseEventArgs e){MoveTo(Position-e.Delta/120*Math.Max(1,SystemInformation.MouseWheelScrollLines));base.OnMouseWheel(e);}
 }
 public class ScrollSurface : Panel {
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
  readonly Control content;readonly Panel viewport=new Panel{Dock=DockStyle.Fill};readonly SlimScrollBar vertical=new SlimScrollBar{Dock=DockStyle.Right};readonly SlimScrollBar horizontal=new SlimScrollBar{Dock=DockStyle.Bottom,Horizontal=true};bool syncing,pending,verticalShown,horizontalShown;int[] visibleRows=new int[0];
  public ScrollSurface(Control control){DoubleBuffered=true;content=control;vertical.Visible=false;horizontal.Visible=false;Controls.Add(viewport);Controls.Add(vertical);Controls.Add(horizontal);viewport.Controls.Add(control);vertical.Change+=ScrollTo;horizontal.Change+=ScrollAcross;var grid=control as SmoothGrid;
   if(grid!=null){control.Dock=DockStyle.Fill;grid.Skin=this;grid.ScrollBars=ScrollBars.None;grid.Scroll+=delegate{BeginSync();};grid.RowsAdded+=delegate{BeginSync();};grid.RowsRemoved+=delegate{BeginSync();};grid.RowStateChanged+=delegate{BeginSync();};grid.ColumnWidthChanged+=delegate{BeginSync();};}
   else if(control is TextBox){control.Dock=DockStyle.Fill;var text=(TextBox)control;text.ScrollBars=ScrollBars.None;text.TextChanged+=delegate{BeginSync();};text.MouseWheel+=delegate{BeginSync();};text.KeyUp+=delegate{BeginSync();};}
   else{control.Dock=DockStyle.None;var scrolling=control as ScrollableControl;if(scrolling!=null){scrolling.AutoScroll=true;var padding=scrolling.Padding;scrolling.Padding=new Padding(padding.Left,padding.Top,padding.Right+SystemInformation.VerticalScrollBarWidth,padding.Bottom+SystemInformation.HorizontalScrollBarHeight);scrolling.Scroll+=delegate{BeginSync();};}control.Layout+=delegate{BeginSync();};}
   viewport.SizeChanged+=delegate{Fit();};control.SizeChanged+=delegate{BeginSync();};control.VisibleChanged+=delegate{BeginSync();};SizeChanged+=delegate{Fit();};}
  void Fit(){if(!(content is DataGridView)&&!(content is TextBox)){content.Bounds=new Rectangle(0,0,viewport.Width+SystemInformation.VerticalScrollBarWidth,viewport.Height+SystemInformation.HorizontalScrollBarHeight);var prior=content.Region;content.Region=new Region(new Rectangle(0,0,Math.Max(1,viewport.Width),Math.Max(1,viewport.Height)));if(prior!=null)prior.Dispose();}BeginSync();}
  void BeginSync(){if(!pending&&IsHandleCreated&&!IsDisposed){pending=true;BeginInvoke(new Action(delegate{pending=false;Sync();}));}}
  public void Wheel(int delta){Sync();ScrollTo(vertical.Position-delta/120*Math.Max(1,SystemInformation.MouseWheelScrollLines));}
  void ScrollAcross(int value){var sc=content as ScrollableControl;if(sc!=null)sc.AutoScrollPosition=new Point(value,-sc.AutoScrollPosition.Y);Sync();}
  void ScrollTo(int value){var grid=content as DataGridView;if(grid!=null){if(visibleRows.Length>0){value=Math.Max(0,Math.Min(value,visibleRows.Length-1));try{grid.FirstDisplayedScrollingRowIndex=visibleRows[value];}catch(InvalidOperationException){}}}else if(content is TextBox&&content.IsHandleCreated){int current=(int)SendMessage(content.Handle,0xCE,IntPtr.Zero,IntPtr.Zero);SendMessage(content.Handle,0xB6,IntPtr.Zero,(IntPtr)(Math.Max(0,value)-current));}else if(content is ListBox){var list=(ListBox)content;if(list.Items.Count>0)list.TopIndex=Math.Max(0,Math.Min(list.Items.Count-1,value));}else{var sc=content as ScrollableControl;if(sc!=null)sc.AutoScrollPosition=new Point(-sc.AutoScrollPosition.X,value);}Sync();content.Invalidate();}
  public void Sync(){if(syncing||IsDisposed)return;syncing=true;try{var grid=content as DataGridView;bool showHorizontal=false;if(grid!=null){visibleRows=grid.Rows.Cast<DataGridViewRow>().Where(r=>r.Visible&&!r.IsNewRow).Select(r=>r.Index).ToArray();vertical.Total=visibleRows.Length;vertical.Page=Math.Max(1,grid.DisplayedRowCount(false));vertical.Position=Math.Max(0,Array.IndexOf(visibleRows,grid.FirstDisplayedScrollingRowIndex));}else if(content is TextBox&&content.IsHandleCreated){vertical.Total=(int)SendMessage(content.Handle,0xBA,IntPtr.Zero,IntPtr.Zero);vertical.Page=Math.Max(1,content.Height/Math.Max(1,content.Font.Height));vertical.Position=(int)SendMessage(content.Handle,0xCE,IntPtr.Zero,IntPtr.Zero);}else if(content is ListBox){var list=(ListBox)content;vertical.Total=list.Items.Count;vertical.Page=Math.Max(1,viewport.Height/Math.Max(1,list.ItemHeight));vertical.Position=list.TopIndex;}else{var sc=content as ScrollableControl;if(sc!=null){vertical.Total=sc.VerticalScroll.Visible?sc.DisplayRectangle.Height:viewport.Height;vertical.Page=viewport.Height;vertical.Position=-sc.AutoScrollPosition.Y;horizontal.Total=sc.HorizontalScroll.Visible?sc.DisplayRectangle.Width:viewport.Width;horizontal.Page=viewport.Width;horizontal.Position=-sc.AutoScrollPosition.X;showHorizontal=sc.HorizontalScroll.Visible;horizontal.Invalidate();}}
   if(horizontalShown!=showHorizontal){horizontalShown=showHorizontal;horizontal.Visible=showHorizontal;}bool show=vertical.Total>vertical.Page;if(verticalShown!=show){verticalShown=show;vertical.Visible=show;Fit();}vertical.Invalidate();}catch(InvalidOperationException){}finally{syncing=false;}}
 }
}
