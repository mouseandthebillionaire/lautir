// RNBO codebox (event-rate) melody picker + deterministic density
//
// Inlets:
// in1: melodyId (0..27)
// in2: density (0..7) 0=>2 hits per half (4 total), 7=>16 per half (32 total, no rests)
// Rest pattern in steps 16..31 mirrors steps 0..15
//
// Outlet:
// listout1: 32 integers, MIDI notes, with -1 for rests

@state melodies = new FixedIntArray(28, 32);
@state melodiesReady: Int = 0;

// Begin Melody

function aMinorStep(idx) {
    // A natural minor from A: A B C D E F G = 0,2,3,5,7,8,10
    idx = idx % 7;
    if (idx == 0) return 0;   // A
    if (idx == 1) return 2;   // B
    if (idx == 2) return 3;   // C
    if (idx == 3) return 5;   // D
    if (idx == 4) return 7;   // E
    if (idx == 5) return 8;   // F
    return 10;                // G
}

function init() {
    var m = 0;
    var i = 0;
    var phrase = 0;
    var degree = 0;
    var note = 0;
    var baseA = 57; // A3
    var useLeadingTone = 0;

    for (m = 0; m < 28; m = m + 1) {
        for (i = 0; i < 32; i = i + 1) {
            phrase = i % 16;

            // 16-note contour (repeats twice over 32 steps)
            // intentionally distinct between first 8 and second 8
            if (phrase == 0) degree = 0;       // A
            else if (phrase == 1) degree = 2;  // C
            else if (phrase == 2) degree = 4;  // E
            else if (phrase == 3) degree = 2;  // C
            else if (phrase == 4) degree = 5;  // F
            else if (phrase == 5) degree = 4;  // E
            else if (phrase == 6) degree = 2;  // C
            else if (phrase == 7) degree = 0;  // A
            else if (phrase == 8) degree = 4;  // E
            else if (phrase == 9) degree = 6;  // G
            else if (phrase == 10) degree = 5; // F
            else if (phrase == 11) degree = 3; // D
            else if (phrase == 12) degree = 1; // B
            else if (phrase == 13) degree = 3; // D
            else if (phrase == 14) degree = 6; // G
            else degree = 0;                    // A cadence

            // Uniqueness across melodies
            degree = (degree + (m % 7)) % 7;

            note = baseA + aMinorStep(degree);

            // Register shaping (ambient, not too wide)
            if ((m % 4) == 1) note = note + 12;
            else if ((m % 4) == 2 && phrase >= 4) note = note + 12;
            else if ((m % 4) == 3 && (phrase == 2 || phrase == 5)) note = note + 12;

            // Harmonic minor color: occasionally raise G -> G#
            // only near cadences, and not every phrase
            useLeadingTone = 0;
            if (degree == 6) {
                if (phrase == 6 || phrase == 7) {
                    if (((m + (i / 8)) % 3) == 0) {
                        useLeadingTone = 1;
                    }
                }
            }
            if (useLeadingTone == 1) {
                note = note + 1; // G -> G#
            }

            // keep range musical/warm
            while (note < 52) note = note + 12; // >= E3
            while (note > 81) note = note - 12; // <= A5

            melodies[m][i] = note;
        }
    }
}

// End Melody

function clampInt(x, lo: Int, hi: Int) {
    let xi: Int = x;
    if (xi < lo) xi = lo;
    if (xi > hi) xi = hi;
    return xi;
}

if (melodiesReady == 0) {
    init();
    melodiesReady = 1;
}

let melodyId: Int = clampInt(in1, 0, 27);
let density: Int = clampInt(in2, 0, 7);
let nHalf: Int = 2 + density * 2; // played positions per 16-step half
if (nHalf > 16) nHalf = 16;

let offset: Int = melodyId % 16;

let out = [];

for (let step: Int = 0; step < 32; step += 1) {
    let p: Int = step % 16;
    let play: Int = 0;
    if (p == 0) play = 1; // downbeat of each half (steps 0 and 16) always plays

    // Evenly space hits around the half-phrase (rests spread out, not clustered)
    for (let k: Int = 0; k < nHalf; k += 1) {
        let s: Int = (offset + (k * 16) / nHalf) % 16;
        if (s == p) play = 1;
    }

    let note: Int = melodies[melodyId][step];
    out.push(play == 1 ? note : -1);
}

listout1 = out;
