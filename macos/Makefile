APP_NAME    := MonitorSuhu
BUILD_DIR   := build
APP         := $(BUILD_DIR)/$(APP_NAME).app
MACOS_DIR   := $(APP)/Contents/MacOS
RES_DIR     := $(APP)/Contents/Resources
SRC_DIR     := Sources
SOURCES     := $(wildcard $(SRC_DIR)/*.swift)
SDK         := $(shell xcrun --sdk macosx --show-sdk-path)
SWIFTC      := xcrun swiftc
ARM_TARGET  := arm64-apple-macosx14.0
X86_TARGET  := x86_64-apple-macosx14.0
SWIFT_FRAMEWORKS := \
	-framework AppKit \
	-framework SwiftUI \
	-framework IOKit \
	-framework Carbon \
	-framework Combine \
	-framework ServiceManagement \
	-framework UserNotifications

TEST_DIR    := Tests
TEST_BIN    := $(BUILD_DIR)/logic-tests
LOGIC_SOURCES := $(SRC_DIR)/Models.swift $(SRC_DIR)/AlertGate.swift $(SRC_DIR)/FullscreenPolicy.swift $(SRC_DIR)/Versioning.swift $(SRC_DIR)/ReleaseAssets.swift $(TEST_DIR)/LogicTests.swift

.PHONY: all app run clean dmg test

all: app

app: $(APP)/Contents/MacOS/$(APP_NAME)

$(APP)/Contents/MacOS/$(APP_NAME): $(SOURCES) Resources/Info.plist Resources/AppIcon.icns
	@mkdir -p "$(MACOS_DIR)" "$(RES_DIR)" "$(BUILD_DIR)"
	@cp Resources/Info.plist "$(APP)/Contents/Info.plist"
	@cp Resources/AppIcon.icns "$(RES_DIR)/AppIcon.icns"
	@printf 'APPL????' > "$(APP)/Contents/PkgInfo"
	$(SWIFTC) -parse-as-library -O \
		-target $(ARM_TARGET) \
		-sdk "$(SDK)" \
		-o "$(BUILD_DIR)/$(APP_NAME)-arm64" \
		$(SWIFT_FRAMEWORKS) \
		$(SOURCES)
	$(SWIFTC) -parse-as-library -O \
		-target $(X86_TARGET) \
		-sdk "$(SDK)" \
		-o "$(BUILD_DIR)/$(APP_NAME)-x86_64" \
		$(SWIFT_FRAMEWORKS) \
		$(SOURCES)
	lipo -create \
		"$(BUILD_DIR)/$(APP_NAME)-arm64" \
		"$(BUILD_DIR)/$(APP_NAME)-x86_64" \
		-output "$@"

test: $(TEST_BIN)
	"$(TEST_BIN)"

$(TEST_BIN): $(LOGIC_SOURCES)
	@mkdir -p "$(BUILD_DIR)"
	$(SWIFTC) -parse-as-library -O \
		-target $(ARM_TARGET) \
		-sdk "$(SDK)" \
		-o "$@" \
		-framework Foundation \
		-framework SwiftUI \
		$(LOGIC_SOURCES)

run: app
	open "$(APP)"

dmg:
	bash scripts/make-dmg.sh

clean:
	rm -rf "$(BUILD_DIR)" dist
