/**
 * The uplift kit — see the UI uplift spec §2/§3.
 *
 * Six techniques, applied consistently. The consistency is the point: used on one screen they read
 * as an inconsistency rather than a treatment, which is why these ship as shared components and
 * utility classes rather than per-page CSS.
 *
 * The utility half (edge-light, rail-*, shadow-volt, press, shimmer, bar-grow) lives in index.css.
 * The hooks live in shared/hooks/useCountUp so this file exports only components and fast refresh
 * keeps working.
 */
export { GrainOverlay } from './GrainOverlay'
export { Bloom } from './Bloom'
export { CountUp } from './CountUp'
