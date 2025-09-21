#/mnt/usb_nvme_2tb/Data/laion2b_en_aesthetic_wds/!/bin/bash

# Build and Deploy Script for Hollow Knight Silksong Mods
# Builds all hkss-* mods and deploys to Steam Deck and local PC

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'

NC='\033[0m' # No Color

# Paths
#STEAMDECK_HOST="deck@100.126.74.7"
STEAMDECK_HOST="deck@192.168.1.133"

STEAMDECK_PATH="/home/deck/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins"
LOCAL_PATH="/home/sam/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins"
RELEASES_DIR="releases"

echo -e "${GREEN}=== Hollow Knight Silksong Mods Build & Deploy ===${NC}"
echo ""

# Create releases directory if it doesn't exist
mkdir -p "$RELEASES_DIR"

# Counter for successful builds
SUCCESS_COUNT=0
TOTAL_COUNT=0

# Build all hkss-* mods
for dir in hkss-*/; do
    if [ -d "$dir" ]; then
        TOTAL_COUNT=$((TOTAL_COUNT + 1))
        MOD_NAME=$(basename "$dir")
        echo -e "${YELLOW}Building $MOD_NAME...${NC}"

        if cd "$dir" && dotnet build -c Release > /dev/null 2>&1; then
            # Find and copy the built DLL to releases
            DLL_PATH="bin/BepInEx/plugins/netstandard2.1/HKSS.*.dll"
            if ls $DLL_PATH 1> /dev/null 2>&1; then
                cp $DLL_PATH "../$RELEASES_DIR/"
                echo -e "${GREEN}✓ Built $MOD_NAME successfully${NC}"
                SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
            else
                echo -e "${RED}✗ Build succeeded but DLL not found for $MOD_NAME${NC}"
            fi
            cd ..
        else
            echo -e "${RED}✗ Failed to build $MOD_NAME${NC}"
            cd ..
        fi
    fi
done

echo ""
echo -e "${GREEN}Built $SUCCESS_COUNT/$TOTAL_COUNT mods successfully${NC}"
echo ""

# Deploy to local PC
echo -e "${YELLOW}Deploying to local PC...${NC}"
if [ -d "$LOCAL_PATH" ]; then
    cp "$RELEASES_DIR"/HKSS.*.dll "$LOCAL_PATH/" 2>/dev/null && \
        echo -e "${GREEN}✓ Deployed to local PC${NC}" || \
        echo -e "${RED}✗ Failed to deploy to local PC${NC}"
else
    echo -e "${RED}✗ Local path not found: $LOCAL_PATH${NC}"
fi

# Deploy to Steam Deck
echo -e "${YELLOW}Deploying to Steam Deck...${NC}"
echo "  Connecting to $STEAMDECK_HOST..."

# Check if Steam Deck is reachable
if ssh -o ConnectTimeout=5 -o BatchMode=yes "$STEAMDECK_HOST" exit 2>/dev/null; then
    # Use rsync to copy files
    if rsync -avz --progress "$RELEASES_DIR"/HKSS.*.dll "$STEAMDECK_HOST:$STEAMDECK_PATH/" 2>/dev/null; then
        echo -e "${GREEN}✓ Deployed to Steam Deck${NC}"
    else
        echo -e "${RED}✗ Failed to deploy to Steam Deck (rsync failed)${NC}"
    fi
else
    echo -e "${RED}✗ Cannot connect to Steam Deck at $STEAMDECK_HOST${NC}"
    echo "  Make sure the Steam Deck is on and SSH is enabled"
fi

echo ""
echo -e "${GREEN}=== Build & Deploy Complete ===${NC}"
echo ""
echo "Mod DLLs available in: $RELEASES_DIR/"
ls -lh "$RELEASES_DIR"/HKSS.*.dll 2>/dev/null | awk '{print "  " $9 " (" $5 ")"}'
