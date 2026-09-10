// Presentation only. No outcome RNG, stop selection, or payout evaluation here.
let reference, context, enabled = false, previousFocus, modalFrame;
const notes = { spin: [110, 165], stop: [220], win: [440, 554.37, 659.25], rise: [329.63, 440, 659.25], feature: [220, 329.63, 440, 659.25], complete: [440, 554.37, 659.25, 880] };

function keyboard(event) {
    const dialog = document.querySelector('[role="dialog"]');
    if (dialog) {
        if (event.code === 'Escape') { event.preventDefault(); reference?.invokeMethodAsync('CloseRules'); }
        if (event.code === 'Tab') {
            const items = [...dialog.querySelectorAll('button, a[href], [tabindex="0"]')];
            const first = items[0], last = items.at(-1);
            if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
            else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
        }
        return;
    }
    if (event.code !== 'Space' || event.repeat || /INPUT|TEXTAREA|SELECT|BUTTON|A|SUMMARY/.test(event.target.tagName) || event.target.isContentEditable) return;
    event.preventDefault();
    reference?.invokeMethodAsync('KeyboardSpin');
}

export function connect(dotnet) {
    reference = dotnet;
    document.addEventListener('keydown', keyboard);
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

export async function sound(on) {
    enabled = on;
    if (!enabled) return;
    try {
        context ??= new (window.AudioContext || window.webkitAudioContext)();
        await context.resume();
        cue('stop');
    } catch { enabled = false; }
}

export function cue(kind) {
    if (!enabled || !context || context.state !== 'running') return;
    const now = context.currentTime;
    (notes[kind] || []).forEach((frequency, i) => {
        const oscillator = context.createOscillator(), gain = context.createGain();
        const start = now + i * 0.07, duration = kind === 'stop' ? 0.075 : 0.22;
        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(frequency, start);
        gain.gain.setValueAtTime(0, start);
        gain.gain.linearRampToValueAtTime(0.045, start + 0.008);
        gain.gain.exponentialRampToValueAtTime(0.001, start + duration);
        oscillator.connect(gain).connect(context.destination);
        oscillator.start(start);
        oscillator.stop(start + duration + 0.02);
        oscillator.onended = () => { oscillator.disconnect(); gain.disconnect(); };
    });
}

export function openDialog() {
    previousFocus = document.activeElement;
    document.body.style.overflow = 'hidden';
    const focus = () => {
        const dialog = document.querySelector('[role="dialog"]');
        if (dialog) dialog.querySelector('button')?.focus();
        else modalFrame = requestAnimationFrame(focus);
    };
    modalFrame = requestAnimationFrame(focus);
}

export function closeDialog() {
    cancelAnimationFrame(modalFrame);
    document.body.style.overflow = '';
    previousFocus?.focus();
}

export function disconnect() {
    document.removeEventListener('keydown', keyboard);
    closeDialog();
    reference = null;
    if (context) { context.close(); context = null; }
}
