#!/usr/bin/env bash
# ==============================================================================
# Script cài đặt tự động 1 lệnh duy nhất cho ShopHoa trên Linux
# Sử dụng: curl -fsSL https://raw.githubusercontent.com/tranminhnhut0308/PM_Shop_Hoa/main/install.sh | bash
# ==============================================================================

set -e

# Màu sắc hiển thị
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}======================================================${NC}"
echo -e "${GREEN}   BẮT ĐẦU CÀI ĐẶT PHẦN MỀM QUẢN LÝ SHOP HOA LINUX   ${NC}"
echo -e "${CYAN}======================================================${NC}"

# 1. Cấu hình đường dẫn tải và thư mục cài đặt
REPO="tranminhnhut0308/PM_Shop_Hoa"
RELEASE_URL="https://github.com/${REPO}/releases/latest/download/ShopHoa-linux.tar.gz"
RAW_FALLBACK_URL="https://raw.githubusercontent.com/${REPO}/main/Releases/linux-x64/ShopHoa-linux.tar.gz"
INSTALL_DIR="$HOME/.local/share/shophoa"
DESKTOP_DIR="$HOME/.local/share/applications"

# 2. Cài đặt các thư viện đồ họa phụ thuộc nếu dùng Debian / Ubuntu
echo -e "\n${YELLOW}[1/4] Kiểm tra thư viện đồ họa hệ thống...${NC}"
if command -v apt-get >/dev/null 2>&1; then
    MISSING_LIBS=""
    for lib in libfontconfig1 libx11-6 libx11-xcb1 libxcursor1 libxi6 libgl1; do
        if ! dpkg -s "$lib" >/dev/null 2>&1; then
            MISSING_LIBS="$MISSING_LIBS $lib"
        fi
    done

    if [ -n "$MISSING_LIBS" ]; then
        echo -e "Đang tự động cài các thư viện phụ thuộc:$MISSING_LIBS..."
        sudo apt-get update -qq && sudo apt-get install -y -qq $MISSING_LIBS >/dev/null 2>&1 || true
    else
        echo -e "${GREEN}Thư viện hệ thống đã đầy đủ.${NC}"
    fi
fi

# 3. Tải và giải nén phần mềm trực tiếp vào thư mục cài đặt
echo -e "\n${YELLOW}[2/4] Đang tải phần mềm từ GitHub...${NC}"
mkdir -p "$INSTALL_DIR"

TEMP_TAR=$(mktemp /tmp/shophoa-XXXXXX.tar.gz)

download_file() {
    local url="$1"
    local dest="$2"
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$url" -o "$dest"
    elif command -v wget >/dev/null 2>&1; then
        wget -q "$url" -O "$dest"
    else
        return 1
    fi
}

# Thử tải từ GitHub Releases (bản mới nhất), fallback về raw repository
if ! download_file "$RELEASE_URL" "$TEMP_TAR"; then
    echo -e "${YELLOW}Đang thử tải từ kho lưu trữ (nhánh main)...${NC}"
    if ! download_file "$RAW_FALLBACK_URL" "$TEMP_TAR"; then
        echo -e "${RED}Lỗi: Không thể tải file cài đặt từ GitHub. Vui lòng kiểm tra lại repository!${NC}"
        rm -f "$TEMP_TAR"
        exit 1
    fi
fi

echo -e "\n${YELLOW}[3/4] Đang giải nén và thiết lập quyền thực thi...${NC}"
tar -xzf "$TEMP_TAR" -C "$INSTALL_DIR"
rm -f "$TEMP_TAR"
chmod +x "$INSTALL_DIR/ShopHoa"

# 4. Tạo biểu tượng Shortcut vào Menu Ứng dụng Linux
echo -e "\n${YELLOW}[4/4] Đang tạo biểu tượng khởi chạy trên Desktop và Menu...${NC}"
mkdir -p "$DESKTOP_DIR"

# Tìm file icon nếu có
ICON_PATH="$INSTALL_DIR/Assets/LOGO_APP.png"
if [ ! -f "$ICON_PATH" ]; then
    ICON_PATH="$INSTALL_DIR/Assets/logo.png"
fi

cat <<EOF > "$DESKTOP_DIR/shophoa.desktop"
[Desktop Entry]
Name=Vựa Hoa Nhà Huy
Comment=Phần mềm Quản lý Shop Hoa
Exec=$INSTALL_DIR/ShopHoa
Icon=$ICON_PATH
Terminal=false
Type=Application
Categories=Office;Finance;
StartupWMClass=ShopHoa
EOF

chmod +x "$DESKTOP_DIR/shophoa.desktop"

# Thêm icon ra màn hình Desktop nếu thư mục Desktop tồn tại
USER_DESKTOP=""
if command -v xdg-user-dir >/dev/null 2>&1; then
    USER_DESKTOP="$(xdg-user-dir DESKTOP 2>/dev/null)"
fi
if [ -z "$USER_DESKTOP" ] && [ -d "$HOME/Desktop" ]; then
    USER_DESKTOP="$HOME/Desktop"
fi

if [ -n "$USER_DESKTOP" ] && [ -d "$USER_DESKTOP" ]; then
    cp "$DESKTOP_DIR/shophoa.desktop" "$USER_DESKTOP/shophoa.desktop"
    chmod +x "$USER_DESKTOP/shophoa.desktop"
    gio set "$USER_DESKTOP/shophoa.desktop" metadata::trusted true 2>/dev/null || true
fi

# Tạo shortcut lệnh ngoài Terminal (chạy trực tiếp bằng gõ 'shophoa')
if [ -d "$HOME/.local/bin" ]; then
    mkdir -p "$HOME/.local/bin"
    ln -sf "$INSTALL_DIR/ShopHoa" "$HOME/.local/bin/shophoa"
fi

echo -e "\n${GREEN}======================================================${NC}"
echo -e "${GREEN}    CÀI ĐẶT THÀNH CÔNG PHẦN MỀM SHOP HOA TRÊN LINUX!  ${NC}"
echo -e "${GREEN}======================================================${NC}"
echo -e "👉 Bạn có thể mở ứng dụng ngay từ ${CYAN}Menu Ứng dụng (Application Launcher)${NC}."
echo -e "👉 Hoặc mở bằng lệnh Terminal: ${CYAN}$INSTALL_DIR/ShopHoa${NC}\n"
