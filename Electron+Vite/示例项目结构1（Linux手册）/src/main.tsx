import { createRoot } from 'react-dom/client'
import App from './App'
import './styles/base.css'
import './styles/app.css'
import './styles/markdown.css'

createRoot(document.getElementById('root') as HTMLElement).render(<App />)
