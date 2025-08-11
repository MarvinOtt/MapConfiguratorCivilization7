export class XoShiRo128plus {
    constructor(seed) {
        this.s = new Uint32Array(4);

        let x = seed >>> 0; // force unsigned 32-bit

        for (let i = 0; i < 4; i++) {
            const { updatedX, output } = XoShiRo128plus._splitMix32(x);
            x = updatedX;       // carry forward updated state
            this.s[i] = output; // scrambled output
        }

        // Ensure state is not all zero
        if (this.s[0] === 0 && this.s[1] === 0 && this.s[2] === 0 && this.s[3] === 0) {
            this.s[0] = 1;
        }
    }

    static _splitMix32(x) {
        x = (x + 0x9E3779B9) >>> 0; // Step 1: increment state
        let z = x;
        z = Math.imul(z ^ (z >>> 16), 0x85EBCA6B) >>> 0; // Step 2
        z = Math.imul(z ^ (z >>> 13), 0xC2B2AE35) >>> 0; // Step 3
        return {
            updatedX: x,
            output: (z ^ (z >>> 16)) >>> 0 // Step 4: final scrambled value
        };
    }

    // Rotate left 32-bit
    static _rotl32(x, k) {
        return ((x << k) | (x >>> (32 - k))) >>> 0;
    }

    // Generate next uint
    nextUint() {
        const result = (this.s[0] + this.s[3]) >>> 0;

        const t = (this.s[1] << 9) >>> 0;

        this.s[2] ^= this.s[0];
        this.s[3] ^= this.s[1];
        this.s[1] ^= this.s[2];
        this.s[0] ^= this.s[3];

        this.s[2] ^= t;

        this.s[3] = XoShiRo128plus._rotl32(this.s[3], 11);

        return result >>> 0;
    }

    // Generate float in [0, 1)
    nextFloat() {
        return (this.nextUint() >>> 8) / (1 << 24);
    }

    // Generate boolean
    nextBool() {
        return (this.nextUint() & 1) === 1;
    }
}