using System;
using System.Drawing;
using System.Windows.Forms;
namespace UnturnedModManager {
 public sealed class Palette {
  public string Id;public Color Background,Surface,Raised,Accent,Text,Muted,Border,Selection,Error,Warning,Success;public bool Dark;
  public Palette(string id,bool dark,string bg,string surface,string raised,string accent,string text,string muted,string border,string selection){Id=id;Dark=dark;Background=C(bg);Surface=C(surface);Raised=C(raised);Accent=C(accent);Text=C(text);Muted=C(muted);Border=C(border);Selection=C(selection);Error=C(dark?"#FF9DA9":"#B42336");Warning=C(dark?"#E9BD80":"#8A531A");Success=C(dark?"#86D6B0":"#176848");}
  static Color C(string value){return ColorTranslator.FromHtml(value);}
 }
 public static class Themes {
  public static readonly Palette[] All={new Palette("midnight",true,"#17141F","#201C2B","#292437","#A78BFA","#F1EBFF","#D0C7DF","#5A506C","#3C3352"),new Palette("graphite",true,"#1D2126","#252A30","#30373F","#9CB4D4","#E9EEF4","#D0DAE6","#5B6878","#39495C"),new Palette("ice",false,"#EDF3F8","#FFFFFF","#E4EDF5","#366A9E","#24364A","#566B7F","#CBD8E5","#DBEAF7"),new Palette("cream",false,"#F7F3EB","#FFFCF6","#EFE8DC","#826A51","#3E3932","#6E655A","#D8CDBE","#EAE0D1")};
  public static Palette Current=All[0];
  public static void Select(string id){Current=Array.Find(All,p=>p.Id==id)??All[0];}
  public static void Apply(Control root){var t=Current;root.BackColor=root.Parent is SectionPanel||root.Parent is ScrollSurface?t.Surface:t.Background;root.ForeColor=t.Text;foreach(Control c in root.Controls){Apply(c);var b=c as Button;if(b!=null){b.BackColor=t.Raised;b.ForeColor=t.Text;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=t.Border;b.FlatAppearance.MouseOverBackColor=t.Selection;b.MinimumSize=new Size(0,36);if(b.Tag as string=="primary"){b.BackColor=t.Accent;b.ForeColor=t.Dark?t.Background:Color.White;}if(b.Tag as string=="danger")b.ForeColor=t.Error;}
    var combo=c as ComboBox;if(combo!=null){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=24;combo.DrawItem-=DrawCombo;combo.DrawItem+=DrawCombo;combo.FlatStyle=FlatStyle.Flat;}
    if(c is TextBoxBase||c is ComboBox||c is ListBox){c.BackColor=t.Raised;c.ForeColor=t.Text;if(c is TextBox)((TextBox)c).BorderStyle=BorderStyle.None;if(c is ListBox)((ListBox)c).BorderStyle=BorderStyle.None;}
    var grid=c as DataGridView;if(grid!=null){grid.BackgroundColor=t.Surface;grid.EnableHeadersVisualStyles=false;grid.GridColor=t.Border;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.DefaultCellStyle.BackColor=t.Surface;grid.DefaultCellStyle.ForeColor=t.Text;grid.DefaultCellStyle.SelectionBackColor=t.Selection;grid.DefaultCellStyle.SelectionForeColor=t.Text;grid.AlternatingRowsDefaultCellStyle.BackColor=t.Raised;grid.ColumnHeadersDefaultCellStyle.BackColor=t.Raised;grid.ColumnHeadersDefaultCellStyle.ForeColor=t.Muted;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=t.Raised;grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=t.Text;grid.DefaultCellStyle.Padding=new Padding(5);foreach(DataGridViewRow row in grid.Rows)if(row.Tag!=null&&row.Tag.GetType().Name=="Row")row.DefaultCellStyle.BackColor=t.Raised;}
   }root.Invalidate(true);
  }
  static void DrawCombo(object sender,DrawItemEventArgs e){var box=(ComboBox)sender;var t=Current;bool selected=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(selected?t.Selection:t.Raised))e.Graphics.FillRectangle(brush,e.Bounds);string text=e.Index<0?box.Text:Convert.ToString(box.Items[e.Index]);TextRenderer.DrawText(e.Graphics,text,e.Font,Rectangle.Inflate(e.Bounds,-4,0),box.Enabled?t.Text:t.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();}
 }
 public sealed class ThemedProgressBar : ProgressBar {
  readonly Timer animation=new Timer{Interval=60};int phase;
  public ThemedProgressBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);animation.Tick+=delegate{phase=(phase+6)%Math.Max(1,Width+80);Invalidate();};}
  public new ProgressBarStyle Style{get{return base.Style;}set{base.Style=value;animation.Enabled=value==ProgressBarStyle.Marquee;Invalidate();}}
  public new int Value{get{return base.Value;}set{base.Value=value;Invalidate();}}
  protected override void OnPaint(PaintEventArgs e){var t=Themes.Current;e.Graphics.Clear(t.Raised);var box=Rectangle.Inflate(ClientRectangle,-1,-1);if(box.Width<1||box.Height<1)return;using(var pen=new Pen(t.Border))e.Graphics.DrawRectangle(pen,box);var fill=Style==ProgressBarStyle.Marquee?new Rectangle(phase-80,2,80,Math.Max(1,Height-4)):new Rectangle(2,2,Math.Max(0,(Width-4)*Value/Math.Max(1,Maximum)),Math.Max(1,Height-4));using(var b=new SolidBrush(t.Accent))e.Graphics.FillRectangle(b,fill);}
  protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
 }
 public sealed class WorkspaceTabs : TabControl {
  protected override void WndProc(ref Message m){if(m.Msg==0x1328){m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 }
 public sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer {
  protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Themes.Current.Surface);}
  protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){if(e.Item.Selected)using(var b=new SolidBrush(Themes.Current.Selection))e.Graphics.FillRectangle(b,new Rectangle(Point.Empty,e.Item.Size));}
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=e.Item.Enabled?Themes.Current.Text:Themes.Current.Muted;base.OnRenderItemText(e);}
 }
}
