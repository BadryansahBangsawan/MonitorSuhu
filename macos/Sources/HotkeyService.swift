import AppKit
import Carbon

final class HotkeyService {
    private var toggleRef: EventHotKeyRef?
    private var editRef: EventHotKeyRef?
    private var handler: EventHandlerRef?
    private var onToggle: (() -> Void)?
    private var onEdit: (() -> Void)?
    private(set) var failedMessage: String?

    func start(toggle: KeyChord, edit: KeyChord, onToggle: @escaping () -> Void, onEdit: @escaping () -> Void) {
        stop()
        self.onToggle = onToggle
        self.onEdit = onEdit
        failedMessage = nil

        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let user = Unmanaged.passUnretained(self).toOpaque()
        InstallEventHandler(GetApplicationEventTarget(), { _, event, userData in
            guard let userData, let event else { return noErr }
            let service = Unmanaged<HotkeyService>.fromOpaque(userData).takeUnretainedValue()
            var hotKeyID = EventHotKeyID()
            GetEventParameter(event, EventParamName(kEventParamDirectObject), EventParamType(typeEventHotKeyID), nil, MemoryLayout<EventHotKeyID>.size, nil, &hotKeyID)
            if hotKeyID.id == 1 { service.onToggle?() }
            if hotKeyID.id == 2 { service.onEdit?() }
            return noErr
        }, 1, &spec, user, &handler)

        let toggleID = EventHotKeyID(signature: fourChar("MSHU"), id: 1)
        let toggleStatus = RegisterEventHotKey(toggle.keyCode, toggle.carbonModifiers, toggleID, GetApplicationEventTarget(), 0, &toggleRef)

        let editID = EventHotKeyID(signature: fourChar("MSHU"), id: 2)
        let editStatus = RegisterEventHotKey(edit.keyCode, edit.carbonModifiers, editID, GetApplicationEventTarget(), 0, &editRef)

        if toggleStatus != noErr && editStatus != noErr {
            failedMessage = "\(toggle.display) and \(edit.display) are already in use."
        } else if toggleStatus != noErr {
            failedMessage = "\(toggle.display) is already in use — overlay toggle was not registered."
        } else if editStatus != noErr {
            failedMessage = "\(edit.display) is already in use — edit-layout was not registered."
        }
    }

    func stop() {
        if let toggleRef { UnregisterEventHotKey(toggleRef) }
        if let editRef { UnregisterEventHotKey(editRef) }
        if let handler { RemoveEventHandler(handler) }
        toggleRef = nil
        editRef = nil
        handler = nil
    }

    deinit { stop() }
}

private func fourChar(_ s: String) -> OSType {
    var result: OSType = 0
    for byte in s.utf8.prefix(4) {
        result = (result << 8) | OSType(byte)
    }
    return result
}
