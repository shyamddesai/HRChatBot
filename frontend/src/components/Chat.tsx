import { useState, useRef, useEffect, useMemo } from 'react';
import { Send, Bot, X, Database, ChevronDown, ChevronUp, Table, FileText, Sparkles} from 'lucide-react';
import api from '../api';

interface Message {
  role: 'user' | 'assistant';
  content: string;
  type?: 'chat' | 'data' | 'error';
  sql?: string;
  rowCount?: number;
  data?: any[];
}

interface SuggestionChip {
  label: string;
  query: string;
  icon?: React.ReactNode;
  role: 'all' | 'HR' | 'Employee';
  category: 'query' | 'action' | 'document';
}

export default function Chat({ onClose }: { onClose?: () => void }) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showSuggestions, setShowSuggestions] = useState(true);
  const [activeCategory, setActiveCategory] = useState<string>('all');
  const scrollRef = useRef<HTMLDivElement>(null);

  // Get user role from localStorage
  const userStr = localStorage.getItem('user');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role || 'Employee';

  useEffect(() => {
    scrollRef.current?.scrollTo(0, scrollRef.current.scrollHeight);
  }, [messages, isLoading]);

   const suggestionChips: SuggestionChip[] = useMemo(() => [
    // Universal queries (all roles)
    { label: 'My profile', query: 'Show my profile information', role: 'all', category: 'query', icon: '👤' },
    { label: 'My salary', query: 'What is my current salary?', role: 'all', category: 'query', icon: '💰' },
    { label: 'Leave balance', query: 'What is my remaining leave balance?', role: 'all', category: 'query', icon: '🏖️' },
    
    // Employee-specific
    { label: 'Request leave', query: 'I want to request annual leave starting next Monday for 5 days', role: 'Employee', category: 'action', icon: '📅' },
    { label: 'My department', query: 'Who is in my department?', role: 'Employee', category: 'query', icon: '🏢' },
    { label: 'Loan eligibility', query: 'Am I eligible for a car loan?', role: 'Employee', category: 'query', icon: '🚗' },
    { label: 'Download payslip', query: 'Generate my payslip for this month', role: 'Employee', category: 'document', icon: '📄' },
    
    // HR-specific
    { label: 'All employees', query: 'List all active employees', role: 'HR', category: 'query', icon: '👥' },
    { label: 'IT department', query: 'Show all employees in IT department', role: 'HR', category: 'query', icon: '💻' },
    { label: 'High earners', query: 'Who earns more than 15000 AED?', role: 'HR', category: 'query', icon: '💎' },
    { label: 'Senior staff', query: 'Show employees with grade 12 and above', role: 'HR', category: 'query', icon: '⭐' },
    { label: 'Pending leaves', query: 'List all pending leave requests', role: 'HR', category: 'query', icon: '⏳' },
    { label: 'Salary report', query: 'What is the average salary by department?', role: 'HR', category: 'query', icon: '📊' },
    { label: 'New hire', query: 'Help me create a new employee record', role: 'HR', category: 'action', icon: '➕' },
  ], []);

  // Filter chips by role and category
  const filteredChips = useMemo(() => {
    return suggestionChips.filter(chip => {
      const roleMatch = chip.role === 'all' || chip.role === userRole;
      const categoryMatch = activeCategory === 'all' || chip.category === activeCategory;
      return roleMatch && categoryMatch;
    });
  }, [suggestionChips, userRole, activeCategory]);

  const categories = [
    { id: 'all', label: 'All', icon: <Sparkles size={14} /> },
    { id: 'query', label: 'Queries', icon: <Database size={14} /> },
    { id: 'action', label: 'Actions', icon: <Send size={14} /> },
    { id: 'document', label: 'Documents', icon: <FileText size={14} /> },
  ];

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

    const handleChipClick = (query: string) => {
        sendMessage(query);
    };

    const resetChat = () => {
        setMessages([]);
        setShowSuggestions(true);
        setInput('');
    };

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
        <div className="flex items-center gap-2">
          {messages.length > 0 && (
            <button 
              onClick={resetChat}
              className="text-xs text-gray-400 hover:text-white px-2 py-1 rounded hover:bg-gray-700 transition"
            >
              New Chat
            </button>
          )}
          {onClose && (
            <button onClick={onClose} className="text-gray-400 hover:text-white transition">
              <X size={20} />
            </button>
          )}
        </div>
      </div>

      {/* Messages area */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-hide">
        {messages.length === 0 && showSuggestions && (
          <div className="space-y-4">
            {/* Welcome message */}
            <div className="text-center text-gray-400 text-sm mb-6">
              <p className="mb-2 text-lg">👋 Welcome, {user?.fullName?.split(' ')[0] || 'User'}!</p>
              <p className="text-xs opacity-75">Choose a quick action or type your question</p>
            </div>

            {/* Category filters */}
            <div className="flex flex-wrap gap-2 justify-center mb-4">
              {categories.map(cat => (
                <button
                  key={cat.id}
                  onClick={() => setActiveCategory(cat.id)}
                  className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs transition-all ${
                    activeCategory === cat.id
                      ? 'bg-blue-600 text-white'
                      : 'bg-[#252a36] text-gray-400 hover:bg-[#2d3341] hover:text-gray-200'
                  }`}
                >
                  {cat.icon}
                  {cat.label}
                </button>
              ))}
            </div>

            {/* Suggestion chips grid */}
            <div className="grid grid-cols-2 gap-2">
              {filteredChips.map((chip, i) => (
                <button
                  key={i}
                  onClick={() => handleChipClick(chip.query)}
                  className="flex items-center gap-2 p-3 text-left text-sm text-gray-300 bg-[#252a36] hover:bg-[#2d3341] hover:text-white rounded-lg border border-gray-700 hover:border-blue-500/50 transition-all group"
                >
                  <span className="text-lg group-hover:scale-110 transition-transform">{chip.icon}</span>
                  <span className="font-medium">{chip.label}</span>
                </button>
              ))}
            </div>

            {/* Role indicator */}
            <div className="text-center mt-4">
              <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-gray-800 text-xs text-gray-500">
                <span className={`w-1.5 h-1.5 rounded-full ${userRole === 'HR' ? 'bg-purple-500' : 'bg-green-500'}`} />
                {userRole} View
              </span>
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
                <div className="whitespace-pre-wrap">{msg.content}</div>
                
                {msg.type === 'data' && msg.data && msg.data.length > 0 && (
                  <DataTable data={msg.data} rowCount={msg.rowCount} />
                )}
                
                {msg.sql && <SqlToggle sql={msg.sql} />}
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

      {/* Quick chips when chat has started */}
      {messages.length > 0 && !isLoading && (
        <div className="px-4 py-2 bg-[#1c202a] border-t border-gray-800">
          <div className="flex gap-2 overflow-x-auto scrollbar-hide pb-1">
            {filteredChips.slice(0, 4).map((chip, i) => (
              <button
                key={i}
                onClick={() => handleChipClick(chip.query)}
                className="flex-shrink-0 flex items-center gap-1.5 px-3 py-1.5 text-xs text-gray-400 bg-[#0f1115] hover:bg-[#252a36] hover:text-gray-200 rounded-full border border-gray-700 transition"
              >
                <span>{chip.icon}</span>
                {chip.label}
              </button>
            ))}
          </div>
        </div>
      )}

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

// Data Table Component (unchanged)
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
            {expanded ? <>Show less <ChevronUp size={12} /></> : <>Show all <ChevronDown size={12} /></>}
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

// SQL Transparency Toggle (unchanged)
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
          >
            {copied ? 'Copied!' : <FileText size={12} />}
          </button>
        </div>
      )}
    </div>
  );
}

function formatCellValue(value: any): string {
  if (value === null || value === undefined) return '-';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'number') {
    if (value > 1000) return `AED ${value.toLocaleString()}`;
    return value.toString();
  }
  if (typeof value === 'string' && value.match(/^\d{4}-\d{2}-\d{2}/)) {
    return new Date(value).toLocaleDateString();
  }
  return String(value);
}