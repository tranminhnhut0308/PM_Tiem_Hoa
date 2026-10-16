#!/usr/bin/env bash
# ==============================================================================
# Script đóng gói phần mềm ShopHoa trên Linux
# Cách dùng:
#   ./pack.sh -Target linux-x64 -Version 1.0.3
#   hoặc: ./pack.sh linux-x64 1.0.3
# ==============================================================================

set -e

TARGET="linux-x64"
VERSION="1.0.0"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -Target|--target|-target|-t)
            TARGET="$2"
            shift 2
            ;;
        -Version|--version|-version|-v)
            VERSION="$2"
            shift 2
            ;;
        *)
            if [[ -z "$POS_ARG_1" ]]; then
                POS_ARG_1="$1"
                TARGET="$1"
            elif [[ -z "$POS_ARG_2" ]]; then
                POS_ARG_2="$1"
                VERSION="$1"
            fi
            shift
            ;;
    esac
done

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}=== BẮT ĐẦU ĐÓNG GÓI SHOPHOA ===${NC}"
echo -e "${YELLOW}Mục tiêu: $TARGET${NC}"
echo -e "${YELLOW}Phiên bản: $VERSION${NC}"

# Xác định danh sách target cần build
if [[ "$TARGET" == "all" ]]; then
    TARGETS=("win-x64" "linux-x64")
else
    TARGETS=("$TARGET")
fi

# 1. Kiểm tra Velopack CLI (vpk)
echo -e "\n${GREEN}[1/3] Kiểm tra công cụ Velopack CLI (vpk)...${NC}"
export PATH="$PATH:$HOME/.dotnet/tools"
if ! command -v vpk >/dev/null 2>&1; then
    echo -e "${YELLOW}Đang tự động cài đặt công cụ vpk (Velopack CLI)...${NC}"
    dotnet tool install -g vpk || true
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

for T in "${TARGETS[@]}"; do
    echo -e "\n${CYAN}------------------------------------------------------${NC}"
    echo -e "${CYAN}>>> ĐANG ĐÓNG GÓI CHO: $T (Phiên bản: $VERSION) <<<${NC}"
    echo -e "${CYAN}------------------------------------------------------${NC}"

    PUBLISH_DIR="publish/$T"
    RELEASE_DIR="Releases/$T"

    # 2. Xuất bản ứng dụng (Self-contained)
    echo -e "\n${GREEN}[2/3] Đang xuất bản dự án ShopHoa (Release, $T, self-contained)...${NC}"
    rm -rf "$PUBLISH_DIR"

    dotnet publish ShopHoa.csproj -c Release /p:PublishProfile="Properties/PublishProfiles/$T.pubxml" /p:Version="$VERSION"

    # 3. Đóng gói bộ cài đặt
    echo -e "\n${GREEN}[3/3] Đang đóng gói bộ cài đặt cho $T...${NC}"
    mkdir -p "$RELEASE_DIR"

    MAIN_EXE="ShopHoa"
    if [[ "$T" == "win-x64" ]]; then
        MAIN_EXE="ShopHoa.exe"
    fi

    ICON_ARGS=()
    if [[ -f "Assets/logo.ico" ]]; then
        ICON_ARGS=("-i" "Assets/logo.ico")
    elif [[ -f "Assets/LOGO_APP.png" ]]; then
        ICON_ARGS=("-i" "Assets/LOGO_APP.png")
    elif [[ -f "Assets/logo.png" ]]; then
        ICON_ARGS=("-i" "Assets/logo.png")
    fi

    if command -v vpk >/dev/null 2>&1; then
        echo -e "${YELLOW}Đang tạo gói Velopack cho $T...${NC}"
        vpk pack -u ShopHoa -v "$VERSION" -p "$PUBLISH_DIR" -e "$MAIN_EXE" -o "$RELEASE_DIR" --packTitle "ShopHoa" --runtime "$T" "${ICON_ARGS[@]}" || true
    fi

    if [[ "$T" == "linux-x64" ]]; then
        echo -e "${YELLOW}Đang tạo gói ShopHoa-linux.tar.gz phục vụ cài đặt 1 dòng...${NC}"
        tar -czf "$RELEASE_DIR/ShopHoa-linux.tar.gz" -C "$PUBLISH_DIR" .
        cp -f "install.sh" "$RELEASE_DIR/install.sh" 2>/dev/null || true
        echo -e "${GREEN}-> Thành công bộ cài Linux: $RELEASE_DIR/ShopHoa-linux.tar.gz và install.sh${NC}"
    elif [[ "$T" == "win-x64" ]]; then
        echo -e "${GREEN}-> Thành công bộ cài Windows: $RELEASE_DIR/ShopHoa-win-Setup.exe${NC}"
    fi
done

echo -e "\n${GREEN}======================================================${NC}"
echo -e "${GREEN}       TẤT CẢ ĐÓNG GÓI ĐÃ HOÀN TẤT THÀNH CÔNG!        ${NC}"
echo -e "${GREEN}======================================================${NC}"
for T in "${TARGETS[@]}"; do
    echo -e "${CYAN}- Thư mục chứa gói $T: Releases/$T${NC}"
done
echo ""
