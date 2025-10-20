using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace 提示词查看wpf3
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            this.KeyDown += MainWindow_KeyDown;
        }
        // 移动目标目录列表文件
        private string _moveToConfigFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ComfyUIViewer", "MoveToPath.xml");

        // 当前有效移动目录（缓存）
        private string CurrentMoveToPath =>
            comboBox_MoveToPath.SelectedItem is ComboBoxItem ci && Directory.Exists(ci.Tag as string)
                ? ci.Tag as string
                : null;
        private string _configFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ComfyUIViewer", "Fav.xml");
        private CancellationTokenSource _thumbCts;
        private List<SortMode> _sortModes = Enum.GetValues(typeof(SortMode)).Cast<SortMode>().ToList();
        private void MainWindow_Closing(object sender, CancelEventArgs e) {
            SaveFav();
            SaveMoveToPaths();
        }
        private void Btn_PickMoveToPath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择移动目标目录",
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                AddMoveToPath(dlg.SelectedPath);
        }
        private void MenuItem_AddMoveToPath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择移动目标目录",
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                AddMoveToPath(dlg.SelectedPath);
        }
        private void AddMoveToPath(string folder)
        {
            if (!Directory.Exists(folder)) return;

            // 去重
            foreach (ComboBoxItem it in comboBox_MoveToPath.Items)
                if ((it.Tag as string).Equals(folder, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox_MoveToPath.SelectedItem = it; // 已存在就选中
                    return;
                }

            var newItem = new ComboBoxItem
            {
                Content = Path.GetFileName(folder),
                Tag = folder,
                ToolTip = folder
            };
            comboBox_MoveToPath.Items.Add(newItem);
            comboBox_MoveToPath.SelectedItem = newItem;
        }
        private void MenuItem_moveto_yangshi_Click(object sender, RoutedEventArgs e)
        {
            string targetDir = @"U:\aipic\样式\";
            if (targetDir == null)
            {
                MessageBox.Show("请先设置有效的移动目标目录！", "提示");
                return;
            }

            if (ListView1.SelectedItems.Count == 0) return;

            // 1. 复制阶段
            foreach (PngItem item in ListView1.SelectedItems)
            {
                try
                {
                    string fileName = Path.GetFileName(item.FullPath);
                    string destPath = Path.Combine(targetDir, fileName);

                    int counter = 1;
                    while (File.Exists(destPath))
                    {
                        string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                        string ext = Path.GetExtension(fileName);
                        destPath = Path.Combine(targetDir, $"{nameNoExt}_{counter}{ext}");
                        counter++;
                    }
                    File.Copy(item.FullPath, destPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"复制失败：{ex.Message}");
                    return; // 任一失败即终止
                }
            }

            // 2. 统一删除（复用 DeleteSelected 全部逻辑）
            DeleteSelected();
        }
        private void MenuItem_MoveTo_Click(object sender, RoutedEventArgs e)
        {
            string targetDir = CurrentMoveToPath;
            if (targetDir == null)
            {
                MessageBox.Show("请先设置有效的移动目标目录！", "提示");
                return;
            }

            if (ListView1.SelectedItems.Count == 0) return;

            // 1. 复制阶段
            foreach (PngItem item in ListView1.SelectedItems)
            {
                try
                {
                    string fileName = Path.GetFileName(item.FullPath);
                    string destPath = Path.Combine(targetDir, fileName);

                    int counter = 1;
                    while (File.Exists(destPath))
                    {
                        string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                        string ext = Path.GetExtension(fileName);
                        destPath = Path.Combine(targetDir, $"{nameNoExt}_{counter}{ext}");
                        counter++;
                    }
                    File.Copy(item.FullPath, destPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"复制失败：{ex.Message}");
                    return; // 任一失败即终止
                }
            }

            // 2. 统一删除（复用 DeleteSelected 全部逻辑）
            DeleteSelected();
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configFile));
            InitDrives();
            InitSortCombo();
            LoadFav();
            //SelectPathInTree(@"E:\ComfyUI-aki-v1.7\ComfyUI\output\2025-10-16");
            SelectPathInTree(findthenewestpath());
            LoadMoveToPaths();   // 读取历史目录
        }
        private string  findthenewestpath()
        {
            if (!Directory.Exists(@"E:\ComfyUI-aki-v1.7\ComfyUI\output"))
            {
                return ("c:\\");
            }
            var dirs=Directory.GetDirectories(@"E:\ComfyUI-aki-v1.7\ComfyUI\output")
                .Select(d=>new DirectoryInfo(d)).OrderByDescending(d=>d.LastWriteTimeUtc).ToArray();
            return (dirs[0].FullName);

        }
        private void SaveMoveToPaths()
        {
            try
            {
                var doc = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("MoveToPaths"));
                foreach (ComboBoxItem item in comboBox_MoveToPath.Items)
                    doc.Root.Add(new System.Xml.Linq.XElement("Path", item.Tag as string));
                Directory.CreateDirectory(Path.GetDirectoryName(_moveToConfigFile));
                doc.Save(_moveToConfigFile);
            }
            catch { }
        }
        private void LoadMoveToPaths()
        {
            comboBox_MoveToPath.Items.Clear();
            if (!File.Exists(_moveToConfigFile)) return;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(_moveToConfigFile);
                foreach (var p in doc.Descendants("Path"))
                {
                    if (Directory.Exists(p.Value))
                        comboBox_MoveToPath.Items.Add(new ComboBoxItem
                        {
                            Content = Path.GetFileName(p.Value),
                            Tag = p.Value,
                            ToolTip = p.Value
                        });
                }
                if (comboBox_MoveToPath.Items.Count > 0)
                    comboBox_MoveToPath.SelectedIndex = 0;
            }
            catch { /* 忽略损坏配置 */ }
        }
        private void InitSortCombo()
        {
            CboSort.ItemsSource = _sortModes.Select(m => m.ToDescription());
            CboSort.SelectedIndex = 3; // TimeDesc
        }
        private void InitDrives()
        {
            TreeView1.Items.Clear();
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var item = new TreeViewItem
                {
                    Header = d.Name,
                    Tag = d.RootDirectory
                };
                if (HasSubDirectories(d.RootDirectory))
                    item.Items.Add(new TreeViewItem { Header = "加载中..." });

                item.Expanded += Drive_Expanded;
                //item.Collapsed += Drive_Collapsed;   // ← 新增
                TreeView1.Items.Add(item);
            }
        }
        private static bool HasSubDirectories(DirectoryInfo dir)
        {
            try
            {
                return dir.GetDirectories().Length > 0;
            }
            catch { return false; }
        }
        private void Drive_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;
            var dir = (DirectoryInfo)item.Tag;

            // 1. 每次都清空
            item.Items.Clear();

            // 2. 重新读盘
            try
            {
                foreach (var sub in dir.GetDirectories())
                {
                    var child = new TreeViewItem
                    {
                        Header = sub.Name,
                        Tag = sub
                    };
                    if (HasSubDirectories(sub))
                        child.Items.Add(new TreeViewItem { Header = "加载中..." });

                    child.Expanded += Drive_Expanded;   // 继续挂事件                    
                    item.Items.Add(child);
                }
            }
            catch { /* 无权限 */ }

            e.Handled = true;
        }
        private void TreeView2_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem node)
            {
                TextBox1.Text = node.Header?.ToString() ?? string.Empty;
            }
        }
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (TreeView1.SelectedItem is TreeViewItem tvi && tvi.Tag is DirectoryInfo dir)
                await LoadThumbnailsIncremental(dir);
        }
        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem tvi && tvi.Tag is DirectoryInfo dir)
            {
                //_ = LoadThumbnails(dir);
                _ = LoadThumbnailsIncremental(dir);
                LabelInfo.Content = dir.FullName;
            }
        }
        private void CboFav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboFav.SelectedItem is ComboBoxItem item && item.Tag is string path)
                SelectPathInTree(path);
        }
        private void BtnAddFav_Click(object sender, RoutedEventArgs e)
        {
            if (TreeView1.SelectedItem is TreeViewItem tvi && tvi.Tag is DirectoryInfo dir)
            {
                string path = dir.FullName;
                if (CboFav.Items.Cast<ComboBoxItem>().Any(i => i.Tag as string == path)) return;
                var item = new ComboBoxItem { Content = dir.Name, Tag = path };
                CboFav.Items.Add(item);
                CboFav.SelectedIndex = CboFav.Items.Count - 1;
            }
        }
        private void LoadFav()
        {
            if (!File.Exists(_configFile)) return;
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(_configFile);
                foreach (var p in doc.Descendants("Path"))
                {
                    var dir = new DirectoryInfo(p.Value);
                    if (dir.Exists)
                        CboFav.Items.Add(new ComboBoxItem { Content = dir.Name, Tag = dir.FullName });
                }
                if (CboFav.Items.Count > 0) CboFav.SelectedIndex = 0;
            }
            catch { }
        }
        public static class RecyclableFile
        {
            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

            private const int FO_DELETE = 3;
            private const int FOF_ALLOWUNDO = 0x40;
            private const int FOF_NOCONFIRMATION = 0x10;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct SHFILEOPSTRUCT
            {
                public IntPtr hwnd;
                public int wFunc;
                public string pFrom;
                public string pTo;
                public short fFlags;
                public bool fAnyOperationsAborted;
                public IntPtr hNameMappings;
                public string lpszProgressTitle;
            }

            public static void Delete(string path)
            {
                var op = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = path + '\0',
                    fFlags = (short)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION)
                };
                int ret = SHFileOperation(ref op);
                if (ret != 0) throw new IOException("删除失败，错误码 " + ret);
            }
        }
        private void BtnDelImg_Click(object sender, RoutedEventArgs e) => DeleteSelected();
        private int _batchSize = 30;            // 每批数量
        private int _currentBatch = 0;          // 当前已加载批次
        private FileInfo[] _allFiles;           // 全文件列表（只读）
        private CancellationTokenSource _loadCts;
        private async void DeleteSelected()
        {
            if (ListView1.SelectedItems.Count == 0) return;

            var items = ListView1.SelectedItems.Cast<PngItem>().ToList();
            var list = (List<PngItem>)ListView1.ItemsSource;

            /* 1. 记下“第一张被删项”在列表中的索引，用于后面补位 */
            int firstIdx = list.IndexOf(items[0]);

            /* 2. 删除磁盘文件 */
            foreach (var it in items)
            {
                try
                {
                    if (!File.Exists(it.FullPath))
                    {
                        list.Remove(it);
                        continue;
                    }

                    // 如果当前大图正是它，先清掉，解除文件占用
                    if (PictureBox1.Source is BitmapImage bmp
                        && bmp.UriSource != null
                        && bmp.UriSource.LocalPath == it.FullPath)
                    {
                        PictureBox1.Source = null;
                    }

                    RecyclableFile.Delete(it.FullPath);
                    list.Remove(it);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败：{ex.Message}");
                    return;          // 任一失败就终止
                }
            }

            /* 3. 重新绑定列表 */
            ListView1.ItemsSource = list.ToList();

            /* 4. 计算补位索引 */
            if (list.Count == 0)                 // 全删光了
            {
                PictureBox1.Source = null;
                TextBox1.Clear();
                return;
            }

            int newSel = firstIdx < list.Count ? firstIdx : list.Count - 1;

            /* 5. 选中并显示 */
            SetSelectedItemAndShow(newSel);

            /* 6. 如果删完不够一屏，继续补一批（保持你原来的逻辑） */
            var sv = FindVisualChild<ScrollViewer>(ListView1);
            if (sv != null && sv.ScrollableHeight <= 0
                && _currentBatch * _batchSize < _allFiles.Length)
                await LoadNextBatch(_loadCts.Token);
        }

        /// <summary>
        /// 选中指定索引项，并把它载入 PictureBox1
        /// </summary>
        /// <summary>
        /// 选中指定索引项，并把它载入 PictureBox1
        /// </summary>
        private void SetSelectedItemAndShow(int index)
        {
            if (index < 0 || index >= ListView1.Items.Count) return;

            // 1. 选中并保证可见
            ListView1.SelectedIndex = index;
            ListView1.ScrollIntoView(ListView1.SelectedItem);

            // 2. 直接加载大图（复用原来 SelectionChanged 里的逻辑）
            if (ListView1.SelectedItem is PngItem item && File.Exists(item.FullPath))
            {
                try
                {
                    // 大图
                    var full = new BitmapImage();
                    full.BeginInit();
                    full.CacheOption = BitmapCacheOption.OnLoad;
                    full.UriSource = new Uri(item.FullPath);
                    full.EndInit();
                    full.Freeze();
                    PictureBox1.Source = full;

                    // 提示词
                    string json = ReadPngTextChunk(item.FullPath, "workflow")
                               ?? ReadPngTextChunk(item.FullPath, "prompt");
                    if (!string.IsNullOrEmpty(json))
                        _ = BuildWorkflowTree(json);
                    else
                    {
                        TreeView2.Items.Clear();
                        TreeView2.Items.Add(new TreeViewItem { Header = "无节点信息" });
                        TextBox1.Clear();
                    }

                    // 标题栏
                    var fi = new FileInfo(item.FullPath);
                    Title = $"{full.PixelWidth}×{full.PixelHeight}  {BytesToHuman(fi.Length)} – {fi.Name}";
                }
                catch (Exception ex)
                {
                    PictureBox1.Source = null;
                    TextBox1.Text = "加载图片失败：\n" + ex.Message;
                }
            }
        }

        private void MenuItem_DeleteSingle_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedItems.Count == 0) return;
            DeleteSelected();   // 直接复用
        }
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // 只响应 Delete 键，并且 ListView 有选中项
            LabelInfo.Content = e.Key.ToString();
            if ((e.Key == Key.Delete || e.Key == Key.Back) &&
                ListView1.SelectedItems.Count > 0)
            {
                e.Handled = true;   // 防止继续冒泡
                DeleteSelected();   // 复用现有逻辑（含补位）
            }
        }
        // 转移（复制 + 复用删除）
        //private void MenuItem_MoveTo_Click(object sender, RoutedEventArgs e)
        //{
        //    if (ListView1.SelectedItems.Count == 0) return;

        //    const string TARGET_DIR = @"U:\aipic\样式";
        //    Directory.CreateDirectory(TARGET_DIR);

        //    // 1. 先复制所有选中项
        //    foreach (PngItem item in ListView1.SelectedItems)
        //    {
        //        try
        //        {
        //            string fileName = Path.GetFileName(item.FullPath);
        //            string destPath = Path.Combine(TARGET_DIR, fileName);

        //            int counter = 1;
        //            while (File.Exists(destPath))
        //            {
        //                string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        //                string ext = Path.GetExtension(fileName);
        //                destPath = Path.Combine(TARGET_DIR, $"{nameNoExt}_{counter}{ext}");
        //                counter++;
        //            }

        //            File.Copy(item.FullPath, destPath, true);
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show($"复制失败：{ex.Message}");
        //            return; // 任一失败就终止整个转移
        //        }
        //    }

        //    // 2. 复制成功后再统一删除（复用同一套删除逻辑）
        //    DeleteSelected();
        //}
        /* 通用视觉树查找助手 */
        // 将 TreeView1 当前选中目录加入移动目标列表
        private void BtnAddMoveTo_Click(object sender, RoutedEventArgs e)
        {
            // 1. 获取当前树选中目录
            if (TreeView1.SelectedItem is TreeViewItem node && node.Tag is DirectoryInfo dir)
            {
                string folder = dir.FullName;
                if (!Directory.Exists(folder))
                {
                    MessageBox.Show("目录不存在！", "提示");
                    return;
                }

                // 2. 加入 ComboBox（自动去重、选中）
                AddMoveToPath(folder);
            }
            else
            {
                MessageBox.Show("请先在左侧目录树中选中一个有效目录！", "提示");
            }
        }
        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t) return t;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
        private void SaveFav()
        {
            var doc = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("Fav"));
            foreach (ComboBoxItem item in CboFav.Items)
                doc.Root.Add(new System.Xml.Linq.XElement("Path", item.Tag as string));
            try { doc.Save(_configFile); } catch { }
        }
        // 保存按钮点击
        private void BtnSavePrompt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TextBox1.Text)) return;

            int idx = TextBox1.Text.IndexOf(':');
            if (idx < 0 || idx == TextBox1.Text.Length - 1)
            {
                //MessageBox.Show("未找到冒号后的文本！", "提示");
                idx = -1;
            }

            string prompt = TextBox1.Text.Substring(idx + 1).Trim();
            if (string.IsNullOrEmpty(prompt)) return;

            try
            {
                string file = System.IO.Path.Combine(
                              System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                              "提示词收集.txt");

                // 追加 + UTF-8
                System.IO.File.AppendAllText(file, prompt + Environment.NewLine);
                MessageBox.Show("已保存到“提示词收集.txt”", "完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message, "错误");
            }
        }
        private async Task LoadThumbnailsIncremental(DirectoryInfo dir)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            await Dispatcher.InvokeAsync(() =>
            {
                ListView1.ItemsSource = null;
                _currentBatch = 0;
            });

            // 只扫文件名，不生成图像
            _allFiles = await Task.Run(() =>
                dir.GetFiles("*.png", SearchOption.TopDirectoryOnly)
                   .OrderByDescending(f => f.LastWriteTime)
                   .ToArray(), token);

            await LoadNextBatch(token);
        }
        private bool _isLoadingBatch = false;
        private async Task LoadNextBatch(CancellationToken token)
        {
            if (_isLoadingBatch) return;
            // 🔒 到底了，直接返回
            if (_currentBatch * _batchSize >= _allFiles.Length) return;

            _isLoadingBatch = true;
            try
            {
                var batch = _allFiles.Skip(_currentBatch * _batchSize)
                                     .Take(_batchSize)
                                     .ToArray();      // ⚠️ 立刻求值，防止延迟执行

                if (batch.Length == 0) return;        // 🔒 空批保护

                var list = ListView1.ItemsSource as List<PngItem> ?? new List<PngItem>();

                foreach (var f in batch)
                {
                    if (token.IsCancellationRequested) break;
                    var item = new PngItem
                    {
                        FullPath = f.FullName,
                        Thumb = await Task.Run(() => GetCachedThumbnail(f.FullName), token)
                    };
                    list.Add(item);
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    ListView1.ItemsSource = list.ToList();
                    _currentBatch++;
                }, DispatcherPriority.Render);
            }
            finally
            {
                _isLoadingBatch = false;
            }
        }
        private CancellationTokenSource _scrollDebounceCts = new CancellationTokenSource();

        private async void ListView1_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_currentBatch * _batchSize >= _allFiles?.Length) return;
            try
    {
                var sv = e.OriginalSource as ScrollViewer;
                if (sv == null) return;

                double advance = 3 * 144;
                bool isBottom = sv.ScrollableHeight > 0 &&
                                sv.VerticalOffset >= sv.ScrollableHeight - advance;

                if (!isBottom && sv.ScrollableHeight <= 0)
                    isBottom = _currentBatch * _batchSize < _allFiles?.Length;

                if (!isBottom || _isLoadingBatch) return;

                _scrollDebounceCts.Cancel();
                _scrollDebounceCts = new CancellationTokenSource();
                var token = _scrollDebounceCts.Token;

                await Task.Delay(100, token);
                if (!token.IsCancellationRequested)
                    await LoadNextBatch(_loadCts.Token);
            }
    catch (OperationCanceledException)
    {
                // 用户快速切换目录或滚动，取消令牌被触发，属于正常情况，直接吞掉
            }

        }
        private readonly Dictionary<string, BitmapImage> _thumbCache = new Dictionary<string, BitmapImage>();
        private readonly object _cacheLock = new object();

        private BitmapImage GetCachedThumbnail(string path)
        {
            lock (_cacheLock)
            {
                if (_thumbCache.TryGetValue(path, out var cached))
                    return cached;
            }

            BitmapImage bmp = null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 128;
                    bmp.StreamSource = fs;
                    bmp.EndInit();
                    bmp.Freeze();
                }
            }
            catch { bmp = CreateErrorThumbnail(); }

            lock (_cacheLock)
            {
                if (!_thumbCache.ContainsKey(path))
                    _thumbCache[path] = bmp;
            }
            return bmp;
        }

        private static BitmapImage CreateErrorThumbnail()
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri("pack://application:,,,/Images/error.png");
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        ////private async Task LoadThumbnails(DirectoryInfo dir)
        ////{
        ////    _thumbCts?.Cancel();
        ////    _thumbCts = new CancellationTokenSource();
        ////    var token = _thumbCts.Token;

        ////    await Dispatcher.InvokeAsync((Action)(() =>
        ////    {
        ////        ListView1.ItemsSource = null;
        ////        Title = $"加载中... {dir.FullName}";
        ////    }));

        ////    // 1. 后台扫文件
        ////    var files = await Task.Run(() =>
        ////        dir.GetFiles("*.png", SearchOption.TopDirectoryOnly)
        ////           .OrderByDescending(f => f.LastWriteTime)
        ////           .ToArray(), token);

        ////    if (token.IsCancellationRequested) return;

        ////    // 2. 分批加载
        ////    var uiList = new List<PngItem>();          // 最终绑定用
        ////    const int batch = 30;
        ////    for (int i = 0; i < files.Length; i += batch)
        ////    {
        ////        if (token.IsCancellationRequested) break;

        ////        var batchFiles = files.Skip(i).Take(batch);
        ////        var batchList = new List<PngItem>();

        ////        // 后台生成缩略图
        ////        await Task.Run(() =>
        ////        {
        ////            foreach (var f in batchFiles)
        ////            {
        ////                try
        ////                {
        ////                    using (var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read))
        ////                    {
        ////                        var bmp = new BitmapImage();
        ////                        bmp.BeginInit();
        ////                        bmp.CacheOption = BitmapCacheOption.OnLoad;
        ////                        bmp.DecodePixelWidth = 128;
        ////                        bmp.StreamSource = fs;
        ////                        bmp.EndInit();
        ////                        bmp.Freeze();

        ////                        batchList.Add(new PngItem
        ////                        {
        ////                            FullPath = f.FullName,
        ////                            Thumb = bmp
        ////                        });
        ////                    }
        ////                }
        ////                catch
        ////                {
        ////                    // 跳过损坏或无法读取的文件
        ////                }
        ////            }
        ////        }, token);

        ////        // 3. 回 UI 一次性追加
        ////        if (!token.IsCancellationRequested && batchList.Count > 0)
        ////        {
        ////            uiList.AddRange(batchList);
        ////            await Dispatcher.BeginInvoke((Action)(() =>
        ////            {
        ////                ListView1.ItemsSource = uiList.ToList();   // 重新绑定
        ////                Title = $"已加载 {uiList.Count}/{files.Length} 张 – {dir.Name}";
        ////            }));
        ////        }
        ////    }

        //    // 全部完成
        //    await Dispatcher.BeginInvoke((Action)(() =>
        //    {
        //        Title = $"共 {uiList.Count} 张 – {dir.Name}";
        //    }));
        //}
        private async void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BitmapImage full;
            if (ListView1.SelectedItem is PngItem item)
            {
                // 大图预览
                if (!File.Exists(item.FullPath))
                {
                    PictureBox1.Source = null;
                    TextBox1.Text = "文件已不存在：\n" + item.FullPath;
                    return;
                }

                try
                {
                     full = new BitmapImage();
                    full.BeginInit();
                    full.CacheOption = BitmapCacheOption.OnLoad;
                    full.UriSource = new Uri(item.FullPath);
                    full.EndInit();
                    full.Freeze();
                    PictureBox1.Source = full;
                }
                catch (Exception ex)
                {
                    PictureBox1.Source = null;
                    TextBox1.Text = "加载图片失败：\n" + ex.Message;
                    return;
                }

                // JSON
                string json = ReadPngTextChunk(item.FullPath, "workflow") ??
                              ReadPngTextChunk(item.FullPath, "prompt");
                if (string.IsNullOrEmpty(json))
                {
                    TreeView2.Items.Clear();
                    TreeView2.Items.Add(new TreeViewItem { Header = "无节点信息" });
                    TextBox1.Clear();
                    return;
                }

                await BuildWorkflowTree(json);

                // 标题栏
                var fi = new FileInfo(item.FullPath);
                Title = $"{full.PixelWidth}×{full.PixelHeight}  {BytesToHuman(fi.Length)} – {fi.Name}";
            }
        }
        private void ListView1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                ListView1.SelectedItem is PngItem item &&
                File.Exists(item.FullPath))
            {
                var data = new DataObject(DataFormats.FileDrop, new[] { item.FullPath });
                DragDrop.DoDragDrop(ListView1, data, DragDropEffects.Copy);
            }
        }
        private async Task BuildWorkflowTree(string json)
        {
            // 1. 后台只做反序列化 + 纯数据计算
            object data = await Task.Run(() =>
            {
                return new JavaScriptSerializer().DeserializeObject(json);
            });

            // 2. 回到 UI 线程再创建/操作 TreeViewItem
            await Dispatcher.BeginInvoke((Action)(() =>
            {
                var root = new TreeViewItem { Header = "workflow" };
                JsonToTreeView(json, root);          // 里面只创建 TreeViewItem
                EnhanceNodeText(root, data, 0, 1);   // 里面只操作 UI 元素

                TreeView2.Items.Clear();
                TreeView2.Items.Add(root);
                root.IsExpanded = true;

                var longest = GetLongestNode(root);
                if (longest != null)
                {
                    TextBox1.Text = longest.Header as string;
                    longest.IsSelected = true;
                    longest.BringIntoView();
                }
            }));
        }
        private TreeViewItem GetLongestNode(TreeViewItem root)
        {
            TreeViewItem longest = root;
            foreach (TreeViewItem child in root.Items)
            {
                var tmp = GetLongestNode(child);
                if ((tmp.Header as string).Length > (longest.Header as string).Length)
                    longest = tmp;
            }
            return longest;
        }
        private string FindTitle(object node, int maxDepth = 2, int depth = 0)
        {
            if (depth > maxDepth || node == null) return null;
            if (node is Dictionary<string, object> d)
            {
                if (d.ContainsKey("_meta") && d["_meta"] is Dictionary<string, object> m && m.ContainsKey("title"))
                    return m["title"]?.ToString();
                if (d.ContainsKey("title")) return d["title"]?.ToString();
                if (d.ContainsKey("class_type")) return d["class_type"]?.ToString();
                foreach (var v in d.Values)
                {
                    var t = FindTitle(v, maxDepth, depth + 1);
                    if (!string.IsNullOrEmpty(t)) return t;
                }
            }
            else if (node is System.Collections.ArrayList l)
            {
                foreach (var v in l)
                {
                    var t = FindTitle(v, maxDepth, depth + 1);
                    if (!string.IsNullOrEmpty(t)) return t;
                }
            }
            return null;
        }
        private void EnhanceNodeText(TreeViewItem parent, object data, int depth, int target)
        {
            if (depth == target && Regex.IsMatch(parent.Header as string, @"^\d+\s*:?\s*$"))
            {
                string title = FindTitle(data);
                if (!string.IsNullOrEmpty(title))
                    parent.Header += " : " + title;
            }

            if (data is Dictionary<string, object> d)
            {
                int i = 0;
                foreach (var kv in d)
                {
                    if (i < parent.Items.Count)
                        EnhanceNodeText((TreeViewItem)parent.Items[i], kv.Value, depth + 1, target);
                    i++;
                }
            }
            else if (data is System.Collections.ArrayList l)
            {
                for (int i = 0; i < l.Count && i < parent.Items.Count; i++)
                    EnhanceNodeText((TreeViewItem)parent.Items[i], l[i], depth + 1, target);
            }
        }
        private void BuildTree(object node, TreeViewItem parent)
        {
            if (node is Dictionary<string, object> dict)
            {
                foreach (var kv in dict)
                {
                    var child = new TreeViewItem { Header = kv.Key };
                    parent.Items.Add(child);
                    BuildTree(kv.Value, child);
                }
            }
            else if (node is System.Collections.ArrayList list)
            {
                int idx = 0;
                foreach (var item in list)
                {
                    string title = "";
                    if (item is Dictionary<string, object> d)
                    {
                        if (d.ContainsKey("title")) title = " : " + d["title"];
                        else if (d.ContainsKey("class_type")) title = " : " + d["class_type"];
                    }
                    var child = new TreeViewItem { Header = idx + title };
                    parent.Items.Add(child);
                    BuildTree(item, child);
                    idx++;
                }
            }
            else
            {
                parent.Header += " : " + node?.ToString();
            }
        }
        private void JsonToTreeView(string json, TreeViewItem parent)
        {
            var obj = new JavaScriptSerializer().DeserializeObject(json);
            BuildTree(obj, parent);
        }
        
        private void TextBox1_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ProcessColonText();


        }
        private enum SortMode
        {
            [Description("文件名正序")] NameAsc,
            [Description("文件名倒序")] NameDesc,
            [Description("时间正序")] TimeAsc,
            [Description("时间倒序")] TimeDesc,
            [Description("大小正序")] SizeAsc,
            [Description("大小倒序")] SizeDesc
        }

        private async void CboSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.ItemsSource == null) return;
            var mode = _sortModes[CboSort.SelectedIndex];
            await Reorder(mode);
        }

        private async Task Reorder(SortMode mode)
        {
            await Task.Run(() =>
            {
                var list = (List<PngItem>)ListView1.ItemsSource;
                switch (mode)
                {
                    case SortMode.NameAsc:
                        list = list.OrderBy(x => x.Name).ToList();
                        break;
                    case SortMode.NameDesc:
                        list = list.OrderByDescending(x => x.Name).ToList();
                        break;
                    case SortMode.TimeAsc:
                        list = list.OrderBy(x => new FileInfo(x.FullPath).LastWriteTime).ToList();
                        break;
                    case SortMode.TimeDesc:
                        list = list.OrderByDescending(x => new FileInfo(x.FullPath).LastWriteTime).ToList();
                        break;
                    case SortMode.SizeAsc:
                        list = list.OrderBy(x => new FileInfo(x.FullPath).Length).ToList();
                        break;
                    case SortMode.SizeDesc:
                        list = list.OrderByDescending(x => new FileInfo(x.FullPath).Length).ToList();
                        break;
                    default:
                        // 不排序
                        break;
                }
                Dispatcher.BeginInvoke((Action)(() => ListView1.ItemsSource = list));
            });
        }
        private void ProcessColonText()
        {
            if (TextBox1.Text.Contains(":"))
            {
                // 使用正则表达式匹配冒号后的所有文本
                Match match = Regex.Match(TextBox1.Text, @":(.+)$");

                if (match.Success)
                {
                    string textAfterColon = match.Groups[1].Value.Trim();

                    if (!string.IsNullOrEmpty(textAfterColon))
                    {
                        // 选中冒号后的文本
                        int colonIndex = TextBox1.Text.IndexOf(':');
                        int selectionStart = colonIndex + 1;
                        int selectionLength = TextBox1.Text.Length - selectionStart;

                        TextBox1.Select(selectionStart, selectionLength);

                        // 复制到剪贴板
                        //Clipboard.SetText(textAfterColon);

                        //MessageBox.Show($"已复制到剪贴板: {textAfterColon}", "提示",
                        //    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            else
            {
                //MessageBox.Show("文本中不包含冒号", "提示",
                //MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private void BtnSearchByLength_Click(object sender, RoutedEventArgs e)
        {
            SearchNextNodeByTextLength();
        }
        private List<TreeViewItem> GetAllFirstLevelNodes(TreeViewItem root)
        {
            var nodes = new List<TreeViewItem>();
            foreach (var item in root.Items)
            {
                if (item is TreeViewItem node)
                {
                    nodes.Add(node);
                }
            }
            return nodes;
        }
        private void SearchNextNodeByTextLength()
        {
            // 验证输入
            if (!int.TryParse(TextBox2.Text, out int minLength))
            {
                MessageBox.Show("请在TextBox2中输入有效的数字", "输入错误");
                return;
            }

            if(TreeView2.Items.Count == 0 || !(TreeView2.Items[0] is TreeViewItem root))
            {
                MessageBox.Show("请先选择一张图片加载工作流数据", "提示");
                return;
            }

            // 获取所有一级子节点
            var firstLevelNodes = GetAllFirstLevelNodes(root);
            if (firstLevelNodes.Count == 0)
            {
                MessageBox.Show("未找到有效的工作流节点", "提示");
                return;
            }

            // 折叠所有节点
            CollapseAllNodes(root);

            int startIndex = _lastLengthSearchIndex + 1;
            if (startIndex >= firstLevelNodes.Count) startIndex = 0;

            // 第一轮搜索
            for (int i = startIndex; i < firstLevelNodes.Count; i++)
            {
                if (CheckNodeTextLength(firstLevelNodes[i], minLength, i))
                {
                    return; // 找到后直接返回
                }
            }

            // 第二轮搜索（循环）
            if (startIndex > 0)
            {
                for (int i = 0; i < startIndex; i++)
                {
                    if (CheckNodeTextLength(firstLevelNodes[i], minLength, i))
                    {
                        return; // 找到后直接返回
                    }
                }
            }

            MessageBox.Show($"未找到文本长度大于 {minLength} 的节点", "提示");
            _lastLengthSearchIndex = -1;
        }
        // 检查节点文本长度（提取冒号后的内容）
        private bool CheckNodeTextLength(TreeViewItem node, int minLength, int nodeIndex)
        {
            string headerText = node.Header?.ToString() ?? "";

            // ====== 例外规则：节点名以“text”开头 ======
            if (headerText.StartsWith("text", StringComparison.OrdinalIgnoreCase))
            {
                //MessageBox.Show(headerText);
                TreeViewItem parent = node.Parent as TreeViewItem;
                parent = parent.Parent as TreeViewItem;
                if (parent != null && MeetClassTypeCondition(parent))
                {
                    // 忽略长度，直接命中
                    //MessageBox.Show("ok");
                    string tempp = ExtractContentAfterColon(headerText);
                    if (tempp=="" ||tempp== "System.Object[]") return false;
                    ProcessFoundNode(node, tempp, nodeIndex);
                    return true;
                }
            }

            // ====== 原长度判断 ======
            string content = ExtractContentAfterColon(headerText);
            if (content.Length > minLength)
            {
                ProcessFoundNode(node, content, nodeIndex);
                return true;
            }

            // ====== 递归子节点 ======
            foreach (var childItem in node.Items)
            {
                if (childItem is TreeViewItem childNode &&
                    CheckNodeTextLength(childNode, minLength, nodeIndex))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断某节点下是否包含二级节点 class_type 的值为
        /// ShowText|pysssss 或 CLIPTextEncode
        /// </summary>
        private bool MeetClassTypeCondition(TreeViewItem parent)
        {
            foreach (var subItem in parent.Items)
            {
                if (subItem is TreeViewItem subNode)
                {
                    string subHeader = subNode.Header?.ToString() ?? "";
                    //MessageBox.Show(subHeader);
                    if(subHeader == "class_type : ShowText|pysssss"||subHeader== "class_type : CLIPTextEncode")
                    {
                        return true;
                    }
                    
                }
            }
            return false;
        }
        // 提取冒号后面的内容
        // 处理找到的节点
        private void ProcessFoundNode(TreeViewItem node, string textContent, int nodeIndex)
        {
            // 展开节点路径
            ExpandNodePath(node);

            // 更新状态
            _lastLengthSearchIndex = nodeIndex;
            TextBox1.Text = textContent;
            node.BringIntoView();

            // 可选：显示找到的信息
            // MessageBox.Show($"找到节点，文本长度: {textContent.Length}", "搜索成功");
        }
        private string ExtractContentAfterColon(string headerText)
        {
            if (string.IsNullOrEmpty(headerText))
                return "";

            int colonIndex = headerText.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < headerText.Length - 1)
            {
                // 提取冒号后面的内容并去除空格
                return headerText.Substring(colonIndex + 1).Trim();
            }

            return ""; // 没有冒号就返回空字符串
        }
        // 递归折叠节点
        private int _lastLengthSearchIndex = -1;
        private void CollapseNodeRecursive(TreeViewItem node)
        {
            node.IsExpanded = false;
            foreach (var childItem in node.Items)
            {
                if (childItem is TreeViewItem childNode)
                {
                    CollapseNodeRecursive(childNode);
                }
            }
        }

        // 折叠所有节点
        private void CollapseAllNodes(TreeViewItem root)
        {
            foreach (var item in root.Items)
            {
                if (item is TreeViewItem node)
                {
                    CollapseNodeRecursive(node);
                }
            }
        }
        // 展开节点路径
        private void ExpandNodePath(TreeViewItem targetNode)
        {
            TreeViewItem current = targetNode;
            var path = new Stack<TreeViewItem>();

            while (current != null)
            {
                path.Push(current);
                current = current.Parent as TreeViewItem;
            }

            while (path.Count > 0)
            {
                TreeViewItem node = path.Pop();
                node.IsExpanded = true;
            }
        }
        
        
        private static uint SwapEndian(uint x) =>
            ((x & 0xff000000) >> 24) | ((x & 0x00ff0000) >> 8) |
            ((x & 0x0000ff00) << 8) | ((x & 0x000000ff) << 24);
        private static string ReadPngTextChunk(string file, string key)
        {
            try
            {
                 var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                 var br = new BinaryReader(fs);
                br.ReadBytes(8);
                while (fs.Position + 4 < fs.Length)
                {
                    uint len = SwapEndian(br.ReadUInt32());
                    string type = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (type == "tEXt")
                    {
                        int keyLen = 0;
                        byte b;
                        while ((b = br.ReadByte()) != 0) keyLen++;
                        fs.Seek(-keyLen - 1, SeekOrigin.Current);
                        string k = Encoding.ASCII.GetString(br.ReadBytes(keyLen));
                        br.ReadByte();
                        byte[] txt = br.ReadBytes((int)(len - keyLen - 1));
                        if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                            return Encoding.UTF8.GetString(txt);
                    }
                    else if (type == "IEND") break;
                    fs.Seek(len + 4, SeekOrigin.Current);
                }
            }
            catch { }
            return null;
        }
        private static string BytesToHuman(long len)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            double v = len;
            int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }
        // 把下面事件挂到 ListView 的 MouseDoubleClick
        private void ListView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListView1.SelectedItem is PngItem it && File.Exists(it.FullPath))
                Process.Start(new ProcessStartInfo(it.FullPath) { UseShellExecute = true });
        }
        private void SelectPathInTree(string path)
        {
            if (!Directory.Exists(path)) return;
            var parts = path.Split('\\');
            ItemsControl curr = TreeView1;
            foreach (var p in parts)
            {
                TreeViewItem found = null;
                foreach (TreeViewItem item in curr.Items)
                    if (item.Header as string == p || item.Header as string == p + "\\")
                    { found = item; break; }
                if (found == null) return;
                found.IsExpanded = true;
                found.BringIntoView();
                curr = found;
            }
            ((TreeViewItem)curr).IsSelected = true;
        }

        private void comboBox_MoveToPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Menu_moveto.Header="移动到"+CurrentMoveToPath.ToString();
        }
    }
    #region 数据项
    public class PngItem
    {
        public string FullPath { get; set; }
        public string Name      // 手动实现，兼容 7.3
        {
            get { return System.IO.Path.GetFileName(FullPath); }
        }
        public BitmapImage Thumb { get; set; }
    }
    public static class EnumEx
    {
        public static string ToDescription(this Enum val)
        {
            var fi = val.GetType().GetField(val.ToString());
            var attr = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));
            return attr == null ? val.ToString() : attr.Description;
        }
    }
    #endregion
}
