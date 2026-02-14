import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Bot, Users, Calendar, Clock, UserPlus, LogOut, Shield, Database } from 'lucide-react';
import api from '../api';
import Chat from './Chat';

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

interface UserInfo {
  role: string;
  fullName: string;
}

export default function Dashboard() {
  const [isChatOpen, setIsChatOpen] = useState(false);
  
  const userStr = localStorage.getItem('user');
  const user: UserInfo | null = userStr ? JSON.parse(userStr) : null;
  const isHR = user?.role === 'HR';

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/';
  };

  const { data: employees, isLoading: employeesLoading } = useQuery({
    queryKey: ['employees'],
    queryFn: async () => {
      const res = await api.get('/employees');
      return res.data as Employee[];
    },
    enabled: isHR // Only fetch all employees if HR
  });

  const { data: myProfile } = useQuery({
    queryKey: ['profile'],
    queryFn: async () => {
      const res = await api.get('/employees/me');
      return res.data;
    }
  });

  const { data: leaves } = useQuery({
    queryKey: ['leaves'],
    queryFn: async () => {
      const res = await api.get('/leaves?status=Pending');
      return res.data as LeaveRequest[];
    },
    enabled: isHR
  });

  const stats = [
    { 
      label: 'Total Employees', 
      value: employees?.length ?? 0, 
      icon: Users, 
      color: 'bg-blue-500',
      visible: isHR 
    },
    { 
      label: 'Pending Leaves', 
      value: leaves?.length ?? 0, 
      icon: Calendar, 
      color: 'bg-yellow-500',
      visible: isHR 
    },
    { 
      label: 'My Department', 
      value: myProfile?.department ?? '-', 
      icon: Database, 
      color: 'bg-green-500',
      visible: !isHR 
    },
    { 
      label: 'My Grade', 
      value: myProfile?.grade ?? '-', 
      icon: Shield, 
      color: 'bg-purple-500',
      visible: !isHR 
    },
  ].filter(s => s.visible);

  return (
    <div className="flex h-screen relative overflow-hidden bg-gray-50 dark:bg-gray-900">
      {/* Main Content Area */}
      <div className={`flex-1 transition-all duration-300 ${isChatOpen ? 'pr-[350px]' : ''} overflow-y-auto`}>
        {/* Header */}
        <header className="bg-white dark:bg-gray-800 shadow-sm sticky top-0 z-10">
          <div className="px-6 py-4 flex justify-between items-center">
            <div>
              <h1 className="text-2xl font-bold text-gray-800 dark:text-white">HR Dashboard</h1>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Welcome back, {user?.fullName} {isHR && <span className="text-blue-500 font-medium">(HR Admin)</span>}
              </p>
            </div>
            <button
              onClick={handleLogout}
              className="p-2 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-full"
            >
              <LogOut size={20} />
            </button>
          </div>
        </header>

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

          {/* Quick Actions for HR */}
          {isHR && (
            <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-4 mb-8">
              <h3 className="text-sm font-semibold text-blue-900 dark:text-blue-300 mb-2">Quick Actions</h3>
              <div className="flex gap-3">
                <button className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded-lg transition">
                  + New Employee
                </button>
                <button className="px-4 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 text-sm rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition">
                  Generate Report
                </button>
                <button className="px-4 py-2 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 text-sm rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition">
                  Approve Leaves
                </button>
              </div>
            </div>
          )}

          {/* Employee Table - HR Only */}
          {isHR && (
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow mb-8">
              <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex justify-between items-center">
                <h2 className="text-lg font-semibold text-gray-800 dark:text-white">All Employees</h2>
                <span className="text-sm text-gray-500">{employees?.length ?? 0} total</span>
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                  <thead className="bg-gray-50 dark:bg-gray-700">
                    <tr>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Name</th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Department</th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Grade</th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase">Status</th>
                    </tr>
                  </thead>
                  <tbody className="bg-white dark:bg-gray-800 divide-y divide-gray-200 dark:divide-gray-700">
                    {employeesLoading ? (
                      <tr><td colSpan={4} className="px-6 py-4 text-center text-gray-500">Loading...</td></tr>
                    ) : employees?.map((emp) => (
                      <tr key={emp.id} className="hover:bg-gray-50 dark:hover:bg-gray-700/50">
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="text-sm font-medium text-gray-900 dark:text-white">{emp.fullName}</div>
                          <div className="text-sm text-gray-500 dark:text-gray-400">{emp.email}</div>
                        </td>
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
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* My Profile - Employee View */}
          {!isHR && myProfile && (
            <div className="bg-white dark:bg-gray-800 rounded-lg shadow mb-8">
              <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
                <h2 className="text-lg font-semibold text-gray-800 dark:text-white">My Profile</h2>
              </div>
              <div className="p-6 grid grid-cols-2 gap-4">
                <div>
                  <label className="text-xs text-gray-500 uppercase">Full Name</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white">{myProfile.fullName}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase">Email</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white">{myProfile.email}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase">Department</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white">{myProfile.department}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase">Grade</label>
                  <p className="text-sm font-medium text-gray-900 dark:text-white">{myProfile.grade}</p>
                </div>
              </div>
            </div>
          )}
        </main>
      </div>

      {/* Floating Chat Button */}
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
        className={`fixed top-0 right-0 h-full w-[400px] transform transition-transform duration-300 ease-in-out z-50 ${
          isChatOpen ? 'translate-x-0' : 'translate-x-full'
        }`}
      >
        <Chat onClose={() => setIsChatOpen(false)} />
      </div>
    </div>
  );
}