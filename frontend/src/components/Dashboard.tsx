import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Bot, Users, Calendar, Clock, UserPlus, LogOut } from 'lucide-react';
import api from '../api';
import Chat from './Chat';

// Types for our data
interface Employee {
  id: string;
  fullName: string;
  email: string;
  department: string;
  grade: string;
  status: string;
}

interface LeaveRequest {
  id: string;
  employeeName: string;
  startDate: string;
  endDate: string;
  type: string;
  status: string;
}

export default function Dashboard() {
  const [isChatOpen, setIsChatOpen] = useState(false);

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/'; // or use navigate from react-router
  };

  // Fetch employees (HR only, but we'll just show if available)
  const { data: employees, isLoading: employeesLoading } = useQuery({
    queryKey: ['employees'],
    queryFn: async () => {
      const res = await api.get('/employees');
      return res.data as Employee[];
    },
    // On error, we can fallback to mock data or show error
  });

  // Fetch pending leave requests
  const { data: leaves, isLoading: leavesLoading } = useQuery({
    queryKey: ['leaves'],
    queryFn: async () => {
      const res = await api.get('/leaves?status=Pending');
      return res.data as LeaveRequest[];
    },
  });

  // Mock stats for demonstration (replace with real data later)
  const stats = [
    { label: 'Total Employees', value: employees?.length ?? 0, icon: Users, color: 'bg-blue-500' },
    { label: 'Pending Leaves', value: leaves?.length ?? 0, icon: Calendar, color: 'bg-yellow-500' },
    { label: 'On Leave Today', value: 3, icon: Clock, color: 'bg-green-500' },
    { label: 'New Hires (This Month)', value: 2, icon: UserPlus, color: 'bg-purple-500' },
  ];

  return (
    <div className="flex h-screen relative overflow-hidden bg-gray-50 dark:bg-gray-900">
      {/* Main Content Area */}
      <div className={`flex-1 transition-all duration-300 ${isChatOpen ? 'pr-[350px]' : ''} overflow-y-auto`}>
        {/* Header */}
        <header className="bg-white dark:bg-gray-800 shadow-sm sticky top-0 z-10">
          <div className="px-6 py-4 flex justify-between items-center">
            <h1 className="text-2xl font-bold text-gray-800 dark:text-white">HR Dashboard</h1>
            <button
                onClick={handleLogout}
                className="p-2 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-full"
                >
                <LogOut size={20} />
                </button>
          </div>
        </header>

        {/* Dashboard Content */}
        <main className="p-6">
          {/* Stats Cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
            {stats.map((stat, idx) => (
              <div key={idx} className="bg-white dark:bg-gray-800 rounded-lg shadow p-6 flex items-center">
                <div className={`${stat.color} w-12 h-12 rounded-lg flex items-center justify-center text-white mr-4`}>
                  <stat.icon size={24} />
                </div>
                <div>
                  <p className="text-sm text-gray-600 dark:text-gray-400">{stat.label}</p>
                  <p className="text-2xl font-semibold text-gray-800 dark:text-white">{stat.value}</p>
                </div>
              </div>
            ))}
          </div>

          {/* Employee Table */}
          <div className="bg-white dark:bg-gray-800 rounded-lg shadow mb-8">
            <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
              <h2 className="text-lg font-semibold text-gray-800 dark:text-white">Employees</h2>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                <thead className="bg-gray-50 dark:bg-gray-700">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">Name</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">Email</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">Department</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">Grade</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider">Status</th>
                  </tr>
                </thead>
                <tbody className="bg-white dark:bg-gray-800 divide-y divide-gray-200 dark:divide-gray-700">
                  {employeesLoading ? (
                    <tr><td colSpan={5} className="px-6 py-4 text-center text-gray-500">Loading...</td></tr>
                  ) : employees && employees.length > 0 ? (
                    employees.map((emp) => (
                      <tr key={emp.id}>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900 dark:text-white">{emp.fullName}</td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">{emp.email}</td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">{emp.department}</td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 dark:text-gray-400">{emp.grade}</td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <span className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${
                            emp.status === 'Active' ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
                          }`}>
                            {emp.status}
                          </span>
                        </td>
                      </tr>
                    ))
                  ) : (
                    <tr><td colSpan={5} className="px-6 py-4 text-center text-gray-500">No employees found.</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Pending Leave Requests */}
          <div className="bg-white dark:bg-gray-800 rounded-lg shadow">
            <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
              <h2 className="text-lg font-semibold text-gray-800 dark:text-white">Pending Leave Requests</h2>
            </div>
            <div className="divide-y divide-gray-200 dark:divide-gray-700">
              {leavesLoading ? (
                <p className="px-6 py-4 text-gray-500">Loading...</p>
              ) : leaves && leaves.length > 0 ? (
                leaves.map((leave) => (
                  <div key={leave.id} className="px-6 py-4 flex items-center justify-between">
                    <div>
                      <p className="text-sm font-medium text-gray-900 dark:text-white">{leave.employeeName}</p>
                      <p className="text-sm text-gray-500 dark:text-gray-400">
                        {leave.type} · {new Date(leave.startDate).toLocaleDateString()} - {new Date(leave.endDate).toLocaleDateString()}
                      </p>
                    </div>
                    <span className="px-2 py-1 text-xs font-semibold rounded-full bg-yellow-100 text-yellow-800">
                      {leave.status}
                    </span>
                  </div>
                ))
              ) : (
                <p className="px-6 py-4 text-gray-500">No pending leaves.</p>
              )}
            </div>
          </div>
        </main>
      </div>

      {/* Floating Chat Button (visible when chat is closed) */}
      {!isChatOpen && (
        <button
          onClick={() => setIsChatOpen(true)}
          className="fixed bottom-6 right-6 w-14 h-14 bg-blue-600 hover:bg-blue-700 rounded-full shadow-lg flex items-center justify-center text-white hover:scale-110 transition-all duration-200 active:scale-95 z-40"
        >
          <Bot size={28} />
        </button>
      )}

      {/* Sliding AI Chat Sidebar */}
      <div
        className={`fixed top-0 right-0 h-full w-[350px] transform transition-transform duration-300 ease-in-out z-50 ${
          isChatOpen ? 'translate-x-0' : 'translate-x-full'
        }`}
      >
        <Chat onClose={() => setIsChatOpen(false)} />
      </div>
    </div>
  );
}