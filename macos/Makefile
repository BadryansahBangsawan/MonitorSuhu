APP_NAME    := MonitorSuhu
BUILD_DIR   := build
APP         := $(BUILD_DIR)/$(APP_NAME).app
MACOS_DIR   := $(APP)/Contents/MacOS
RES_DIR     := $(APP)/Contents/Resources
SRC_DIR     := Sources
SOURCES     := $(wildcard $(SRC_DIR)/*.swift)
SDK         := $(shell xcrun --sdk macosx --show-sdk-path)
TARGET      := arm64-apple-macosx14.0
SWIFTC      := xcrun swiftc

.PHONY: all app run clean dmg

all: app

app: $(APP)/Contents/MacOS/$(APP_NAME)

$(APP)/Contents/MacOS/$(APP_NAME): $(SOURCES) Resources/Info.plist
	@mkdir -p "$(MACOS_DIR)" "$(RES_DIR)"
	@cp Resources/Info.plist "$(APP)/Contents/Info.plist"
	@printf 'APPL????' > "$(APP)/Contents/PkgInfo"
	$(SWIFTC) -parse-as-library -O \
		-target $(TARGET) \
		-sdk "$(SDK)" \
		-o "$@" \
		-framework AppKit \
		-framework SwiftUI \
		-framework IOKit \
		-framework Carbon \
		-framework Combine \
		-framework ServiceManagement \
		$(SOURCES)

run: app
	open "$(APP)"

dmg:
	bash scripts/make-dmg.sh

clean:
	rm -rf "$(BUILD_DIR)" dist
