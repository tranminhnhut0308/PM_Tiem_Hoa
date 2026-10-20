using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShopHoa.Helpers;
using ShopHoa.Models;
using ShopHoa.Services;

namespace ShopHoa.Views;

public partial class ChonHangHoaVaoKyDialog : Window
{
    private readonly DatabaseService _db = new();
    private readonly uint _kyId;
    private readonly HashSet<uint> _daCoSanTrongKy;
    private readonly List<SanPham> _allSanPhams = new();
    private readonly List<SanPham> _filteredSanPhams = new();

    public IReadOnlyList<uint> SelectedSanPhamIds { get; private set; } = new List<uint>();

    public ChonHangHoaVaoKyDialog(uint kyId, IEnumerable<uint> daCoTrongKy)
    {
        InitializeComponent();
        _kyId = kyId;
        _daCoSanTrongKy = new HashSet<uint>(daCoTrongKy ?? Enumerable.Empty<uint>());

        Loaded += async (_, _) => await KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        try
        {
            // Tải nhóm sản phẩm
            var nhoms = await _db.GetNhomSanPhamAsync();
            var fullNhoms = new List<ProductOption>
            {
                new() { Id = 0, Name = "Tất cả nhóm hàng" }
            };
            fullNhoms.AddRange(nhoms);
            NhomComboBox.ItemsSource = fullNhoms;
            NhomComboBox.SelectedIndex = 0;

            // Tải toàn bộ sản phẩm
            var (products, _) = await _db.GetSanPhamPagedAsync("", 1, 10000);

            // Lọc bỏ những sản phẩm đã có sẵn trong kỳ
            _allSanPhams.Clear();
            foreach (var p in products)
            {
                if (!_daCoSanTrongKy.Contains(p.Id))
                {
                    p.IsSelected = false;
                    _allSanPhams.Add(p);
                }
            }

            LocDuLieu();
        }
        catch (Exception ex)
        {
            SafeLog.WriteError("Lỗi khởi tạo ChonHangHoaVaoKyDialog", ex);
        }
    }

    private void LocDuLieu()
    {
        var search = (SearchTextBox.Text ?? "").Trim();
        uint nhomId = 0;
        if (NhomComboBox.SelectedItem is ProductOption opt)
            nhomId = opt.Id;

        _filteredSanPhams.Clear();

        foreach (var p in _allSanPhams)
        {
            bool matchSearch = string.IsNullOrWhiteSpace(search)
                || (p.MaSanPham?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                || (p.TenSanPham?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);

            bool matchNhom = nhomId == 0
                || (p.NhomSanPhamId == nhomId);

            if (matchSearch && matchNhom)
            {
                _filteredSanPhams.Add(p);
            }
        }

        SanPhamDataGrid.ItemsSource = null;
        SanPhamDataGrid.ItemsSource = _filteredSanPhams;

        CapNhatThongKeChon();
    }

    private void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        LocDuLieu();
    }

    private void NhomComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        LocDuLieu();
    }

    private void ChonTatCaCheckBox_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        bool isCheck = ChonTatCaCheckBox.IsChecked == true;
        foreach (var p in _filteredSanPhams)
        {
            p.IsSelected = isCheck;
        }

        SanPhamDataGrid.ItemsSource = null;
        SanPhamDataGrid.ItemsSource = _filteredSanPhams;

        CapNhatThongKeChon();
    }

    private void CapNhatThongKeChon()
    {
        int count = _allSanPhams.Count(p => p.IsSelected);
        ThongKeChonTextBlock.Text = $"Đã chọn {count} mặt hàng";
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var selected = _allSanPhams.Where(p => p.IsSelected).Select(p => p.Id).ToList();
        if (selected.Count == 0)
        {
            ThongKeChonTextBlock.Text = "⚠️ Vui lòng tick chọn ít nhất một mặt hàng.";
            return;
        }

        SelectedSanPhamIds = selected;
        Close(true);
    }
}
