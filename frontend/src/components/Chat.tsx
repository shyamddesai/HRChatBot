import { useState, useRef, useEffect } from 'react';
import { Send, Bot, X, Database, ChevronDown, ChevronUp, Table, FileText } from 'lucide-react';
import api from '../api';

interface Message {
  role: 'user' | 'assistant';
  content: string;
  type?: 'chat' | 'data' | 'error';
  sql?: string;
  rowCount?: number;
  data?: any[];
}

export default function Chat({ onClose }: { onClose?: () => void }) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showSuggestions, setShowSuggestions] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo(0, scrollRef.current.scrollHeight);
  }, [messages, isLoading]);

  const sendMessage = async (text: string = input) => {
    if (!text.trim() || isLoading) return;
    
    const userMsg: Message = { role: 'user', content: text };
    const currentHistory = messages.map(m => ({ role: m.role, content: m.content }));

    setMessages(prev => [...prev, userMsg]);
    setInput('');
    setShowSuggestions(false);
    setIsLoading(true);

    try {
      const res = await api.post('/chat', { message: text, history: currentHistory });
      const botMsg: Message = { 
        role: 'assistant', 
        content: res.data.answer,
        type: res.data.type,
        sql: res.data.sql,
        rowCount: res.data.rowCount,
        data: res.data.data
      };
      setMessages(prev => [...prev, botMsg]);
    } catch (err) {
      console.error("Chat Error:", err);
      setMessages(prev => [...prev, { 
        role: 'assistant', 
        content: "⚠️ Error connecting to HR Service.",
        type: 'error'
      }]);
    } finally {
      setIsLoading(false);
    }
  };

  const suggestions = [
    "Show all employees in IT department",
    "Who earns more than 10000 AED?",
    "List employees with grade higher than 10",
    "What is my leave balance?",
    "How many people were hired this year?"
  ];

  return (
    <div className="flex flex-col h-full bg-[#161920] border-l border-gray-800 shadow-2xl">
      {/* Header */}
      <div className="p-4 border-b border-gray-800 flex justify-between items-center bg-[#1c202a]">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-blue-500/20 flex items-center justify-center">
            <Bot size={18} className="text-blue-400" />
          </div>
          <div>
            <span className="font-semibold text-gray-100 block">HR Assistant</span>
            <span className="text-xs text-gray-500">Powered by Groq AI</span>
          </div>
        </div>
        {onClose && (
          <button onClick={onClose} className="text-gray-400 hover:text-white transition">
            <X size={20} />
          </button>
        )}
      </div>

      {/* Messages area */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-hide">
        {messages.length === 0 && showSuggestions && (
          <div className="space-y-3">
            <div className="text-center text-gray-500 text-sm mb-4">
              <p className="mb-2">👋 Welcome! I can help you query HR data using natural language.</p>
              <p className="text-xs opacity-75">Try asking about employees, salaries, or leave balances.</p>
            </div>
            <div className="space-y-2">
              {suggestions.map((suggestion, i) => (
                <button
                  key={i}
                  onClick={() => sendMessage(suggestion)}
                  className="w-full text-left p-3 text-sm text-gray-300 bg-[#252a36] hover:bg-[#2d3341] rounded-lg border border-gray-700 transition-colors"
                >
                  {suggestion}
                </button>
              ))}
            </div>
          </div>
        )}
        
        {messages.map((msg, i) => (
          <div key={i} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
            <div className={`max-w-[95%] ${msg.role === 'user' ? 'w-auto' : 'w-full'}`}>
              <div className={`p-3 rounded-2xl text-sm ${
                msg.role === 'user' 
                  ? 'bg-blue-600 text-white rounded-tr-none' 
                  : msg.type === 'error'
                    ? 'bg-red-900/30 text-red-200 rounded-tl-none border border-red-800'
                    : 'bg-[#252a36] text-gray-200 rounded-tl-none border border-gray-700'
              }`}>
                {/* Message Content */}
                <div className="whitespace-pre-wrap">{msg.content}</div>
                
                {/* Data Table for structured results */}
                {msg.type === 'data' && msg.data && msg.data.length > 0 && (
                  <DataTable data={msg.data} rowCount={msg.rowCount} />
                )}
                
                {/* SQL Transparency Toggle */}
                {msg.sql && (
                  <SqlToggle sql={msg.sql} />
                )}
              </div>
            </div>
          </div>
        ))}

        {isLoading && (
          <div className="flex justify-start">
            <div className="bg-[#252a36] p-3 rounded-2xl rounded-tl-none border border-gray-700">
              <div className="flex items-center gap-2 text-gray-400 text-sm">
                <div className="flex gap-1">
                  <div className="w-1.5 h-1.5 bg-blue-500 rounded-full animate-bounce"></div>
                  <div className="w-1.5 h-1.5 bg-blue-500 rounded-full animate-bounce [animation-delay:0.2s]"></div>
                  <div className="w-1.5 h-1.5 bg-blue-500 rounded-full animate-bounce [animation-delay:0.4s]"></div>
                </div>
                <span>Thinking...</span>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Input area */}
      <div className="p-4 bg-[#1c202a] border-t border-gray-800">
        <div className="relative">
          <input
            className="w-full bg-[#0f1115] border border-gray-700 rounded-xl px-4 py-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 text-white pr-12 transition-all"
            placeholder="Ask about employees, salaries, or policies..."
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
          />
          <button 
            onClick={() => sendMessage()}
            disabled={isLoading}
            className="absolute right-2 top-2 p-1.5 text-blue-400 hover:text-blue-300 disabled:opacity-50"
          >
            <Send size={18} />
          </button>
        </div>
        <div className="mt-2 text-xs text-gray-500 text-center">
          Press Enter to send • SQL queries are logged for transparency
        </div>
      </div>
    </div>
  );
}

// Data Table Component
function DataTable({ data, rowCount }: { data: any[], rowCount?: number }) {
  const [expanded, setExpanded] = useState(false);
  const columns = Object.keys(data[0]);
  const displayData = expanded ? data : data.slice(0, 3);
  const hasMore = data.length > 3;

  return (
    <div className="mt-3 border-t border-gray-700 pt-3">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2 text-xs text-gray-400">
          <Table size={14} />
          <span>{rowCount || data.length} rows returned</span>
        </div>
        {hasMore && (
          <button 
            onClick={() => setExpanded(!expanded)}
            className="text-xs text-blue-400 hover:text-blue-300 flex items-center gap-1"
          >
            {expanded ? (
              <>Show less <ChevronUp size={12} /></>
            ) : (
              <>Show all <ChevronDown size={12} /></>
            )}
          </button>
        )}
      </div>
      
      <div className="overflow-x-auto">
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-gray-700">
              {columns.map(col => (
                <th key={col} className="text-left py-1 px-2 text-gray-400 font-medium uppercase tracking-wider">
                  {col.replace(/_/g, ' ')}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {displayData.map((row, i) => (
              <tr key={i} className="border-b border-gray-800/50 last:border-0 hover:bg-white/5">
                {columns.map(col => (
                  <td key={col} className="py-1.5 px-2 text-gray-300">
                    {formatCellValue(row[col])}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// SQL Transparency Toggle
function SqlToggle({ sql }: { sql: string }) {
  const [showSql, setShowSql] = useState(false);
  const [copied, setCopied] = useState(false);

  const copyToClipboard = () => {
    navigator.clipboard.writeText(sql);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="mt-3 border-t border-gray-700 pt-2">
      <button 
        onClick={() => setShowSql(!showSql)}
        className="flex items-center gap-2 text-xs text-gray-500 hover:text-gray-300 transition-colors"
      >
        <Database size={12} />
        <span>{showSql ? 'Hide SQL' : 'Show generated SQL'}</span>
        {showSql ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
      </button>
      
      {showSql && (
        <div className="mt-2 relative group">
          <pre className="bg-[#0f1115] border border-gray-700 rounded-lg p-3 text-xs text-green-400 overflow-x-auto font-mono">
            <code>{sql}</code>
          </pre>
          <button
            onClick={copyToClipboard}
            className="absolute top-2 right-2 p-1.5 bg-gray-800 hover:bg-gray-700 rounded text-gray-400 hover:text-white opacity-0 group-hover:opacity-100 transition-opacity"
            title="Copy SQL"
          >
            {copied ? 'Copied!' : <FileText size={12} />}
          </button>
        </div>
      )}
    </div>
  );
}

// Format cell values for display
function formatCellValue(value: any): string {
  if (value === null || value === undefined) return '-';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'number') {
    // Format as currency if it looks like a salary
    if (value > 1000) return `AED ${value.toLocaleString()}`;
    return value.toString();
  }
  if (typeof value === 'string' && value.match(/^\d{4}-\d{2}-\d{2}/)) {
    // Format dates
    return new Date(value).toLocaleDateString();
  }
  return String(value);
}