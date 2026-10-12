import SoftAurora from './components/bits/SoftAurora/SoftAurora';
import { ChatArea } from './components/ChatArea';
import { RightPanel } from './components/RightPanel';
import { Sidebar } from './components/Sidebar';
import WindowChrome from './components/WindowChrome';
import { palette } from './lib/palette';
import { reduceMotion } from './lib/motionPref';
import { useAppStore } from './stores/app';

export default function App() {
  const panelOpen = useAppStore((s) => s.panelOpen);

  return (
    <div className="window" data-panel={panelOpen ? 'open' : 'closed'}>
      <div className="bg-layer" aria-hidden="true">
        {reduceMotion ? (
          <div className="bg-flow" />
        ) : (
          <SoftAurora
            color1={palette.aurora1}
            color2={palette.aurora2}
            speed={0.4}
            scale={2.4}
            brightness={0.8}
            bandHeight={0.34}
            bandSpread={1.7}
            noiseAmplitude={1.25}
            noiseFrequency={2.1}
            colorSpeed={0.6}
            lightMode
            enableMouseInteraction={false}
          />
        )}
      </div>
      <Sidebar />
      <ChatArea />
      {panelOpen && <RightPanel />}
      <WindowChrome />
    </div>
  );
}
