import { createRoot } from 'react-dom/client'
import App from './App'
import './styles/base.css'
import './styles/themes.css'
import './styles/shell.css'
import './styles/window.css'
import './styles/apps.css'

createRoot(document.getElementById('root') as HTMLElement).render(<App />)
