// Native haptics for Deepforge (iOS). Called from Haptics.cs; the int matches the Haptic enum there.
#import <UIKit/UIKit.h>

static UISelectionFeedbackGenerator *sSelection;
static UINotificationFeedbackGenerator *sNotification;
static UIImpactFeedbackGenerator *sImpact[5];

static UIImpactFeedbackGenerator *Impact(int index, UIImpactFeedbackStyle style)
{
    if (!sImpact[index]) sImpact[index] = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    return sImpact[index];
}

extern "C" void _IdleMineHaptic(int type)
{
    switch (type)
    {
        case 0: // Selection
            if (!sSelection) sSelection = [[UISelectionFeedbackGenerator alloc] init];
            [sSelection selectionChanged];
            [sSelection prepare];
            break;
        case 1: { UIImpactFeedbackGenerator *g = Impact(0, UIImpactFeedbackStyleLight); [g impactOccurred]; [g prepare]; break; }
        case 2: { UIImpactFeedbackGenerator *g = Impact(1, UIImpactFeedbackStyleMedium); [g impactOccurred]; [g prepare]; break; }
        case 3: { UIImpactFeedbackGenerator *g = Impact(2, UIImpactFeedbackStyleHeavy); [g impactOccurred]; [g prepare]; break; }
        case 4: { UIImpactFeedbackGenerator *g = Impact(3, UIImpactFeedbackStyleSoft); [g impactOccurred]; [g prepare]; break; }
        case 5: { UIImpactFeedbackGenerator *g = Impact(4, UIImpactFeedbackStyleRigid); [g impactOccurred]; [g prepare]; break; }
        case 6: case 7: case 8:
            if (!sNotification) sNotification = [[UINotificationFeedbackGenerator alloc] init];
            [sNotification notificationOccurred:(type == 6 ? UINotificationFeedbackTypeSuccess
                                               : type == 7 ? UINotificationFeedbackTypeWarning
                                                           : UINotificationFeedbackTypeError)];
            [sNotification prepare];
            break;
    }
}
